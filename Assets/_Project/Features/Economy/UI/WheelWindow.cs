using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // One slice of the drawn wheel: its amount ("150", "×1") and icon (null: the fallback letter of letterOf); an
    // amount of the currency draws its mark instead (currency; null: a booster's, in the slice's label colour)
    public readonly struct WheelSliceLabel
    {
        public WheelSliceLabel(string amount, Sprite icon, string letterOf, CurrencyMark currency = null)
        {
            Amount = amount;
            Icon = icon;
            LetterOf = letterOf;
            Currency = currency;
        }

        public string Amount { get; }
        public Sprite Icon { get; }
        public string LetterOf { get; }
        public CurrencyMark Currency { get; }
    }

    public interface IWheelWindowView
    {
        event Action SpinClicked;
        event Action SpinAdClicked;
        event Action InfoClicked;
        event Action CloseClicked;
        int MaxSlices { get; }
        void SetOpen(bool open);

        // Slice 0 starts at the top and the rest follow clockwise; the wheel stands with slice 0 under the pointer and
        // every label stays upright as it turns
        void ShowSlices(IReadOnlyList<WheelSliceLabel> slices);

        // The free Spin while it is ready, else the ad's Spin while one can be watched; status is the progress, the
        // prize or "not ready", note the ad spins left (null hides it)
        void ShowControls(bool freeReady, bool adShown, string status, string note);

        // Every slice's odds (null hides the panel)
        void ShowOdds(IReadOnlyList<string> lines);

        // Turns the wheel (unscaled time) until the slice stops under the pointer
        UniTask SpinTo(int index, CancellationToken ct);

        void SetBusy(bool busy);
    }

    // The lucky wheel window (E17): the code-drawn slices, the free spin every FreeEvery runs or won levels, the ad
    // spins of the day, and the "i" button with every slice's odds (shown for the free wheel too, §10). The prize is
    // paid when the spin starts; the wheel then turns to it.
    public sealed class WheelWindow : EconomyWindow
    {
        private readonly IWheelWindowView view;
        private readonly Wheel wheel;
        private readonly ILocalizer localizer;
        private readonly System.Random random;
        private IReadOnlyList<WheelSlice> slices = Array.Empty<WheelSlice>();
        private bool oddsShown;
        private string result;

        public WheelWindow(IWheelWindowView view, Economy economy, Wheel wheel, IEconomyAds ads, ILocalizer localizer, IUiFeedback feedback,
            Func<bool> autoPlay, System.Random random) : base(economy, ads, feedback, autoPlay)
        {
            this.view = view;
            this.wheel = wheel;
            this.localizer = localizer;
            this.random = random;
            view.SpinClicked += OnSpin;
            view.SpinAdClicked += OnSpinAd;
            view.InfoClicked += OnInfo;
            view.CloseClicked += OnCloseClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            view.SpinClicked -= OnSpin;
            view.SpinAdClicked -= OnSpinAd;
            view.InfoClicked -= OnInfo;
            view.CloseClicked -= OnCloseClicked;
        }

        // Each slice's prize and its share of the total weight, in the wheel's order
        public IReadOnlyList<string> Odds()
        {
            var lines = new List<string>();
            var odds = WeightedPick.Odds(slices, slice => slice.Weight);
            for (var i = 0; i < slices.Count; i++)
            {
                lines.Add(localizer.Get(EconomyKeys.WheelOddsLine, EconomyText.Reward(localizer, Economy.Config, slices[i]),
                    EconomyText.Percent(localizer, odds[i])));
            }
            return lines;
        }

        protected override bool Available => wheel.Configured;

        protected override void OnOpen()
        {
            var list = new List<WheelSlice>();
            foreach (var slice in wheel.Slices)
                if (list.Count < view.MaxSlices) list.Add(slice);
            slices = list;
            var labels = new List<WheelSliceLabel>();
            foreach (var slice in slices) labels.Add(Label(slice));
            view.ShowSlices(labels);
            oddsShown = false;
            result = null;
            view.ShowOdds(null);
            view.SetBusy(false);
        }

        protected override void SetViewOpen(bool open) => view.SetOpen(open);

        protected override void Refresh()
        {
            var free = wheel.FreeSpinReady;
            var ad = !free && wheel.AdSpinReady;
            var status = result ?? (free
                ? localizer.Get(EconomyKeys.WheelReady)
                : localizer.Get(EconomyKeys.WheelProgress, wheel.Progress, wheel.FreeEvery));
            var note = ad ? localizer.Get(EconomyKeys.WheelAdLeft, wheel.AdSpinsLeft) : null;
            view.ShowControls(free, ad, status, note);
        }

        private WheelSliceLabel Label(WheelSlice slice)
        {
            if (slice.Booster == null) return new WheelSliceLabel(slice.Coins.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null, null, new EconomyCurrency(Economy.Config, localizer).Mark);
            var booster = Economy.Config.Booster(slice.Booster);
            return new WheelSliceLabel(localizer.Get(EconomyKeys.Amount, slice.Count), EconomyIcons.Booster(booster),
                EconomyText.BoosterName(localizer, Economy.Config, slice.Booster));
        }

        private void OnSpin()
        {
            if (!IsOpen || Busy || !wheel.FreeSpinReady) return;
            Feedback.Click();
            SpinAsync(false).Forget();
        }

        private void OnSpinAd()
        {
            if (!IsOpen || Busy || !wheel.AdSpinReady) return;
            Feedback.Click();
            SpinAsync(true).Forget();
        }

        private async UniTaskVoid SpinAsync(bool withAd)
        {
            Busy = true;
            view.SetBusy(true);
            result = null;
            var token = Token;
            try
            {
                var slice = withAd ? await wheel.SpinWithAdAsync(random, token) : wheel.Spin(random);
                if (slice == null)
                {
                    result = withAd ? localizer.Get(EconomyKeys.AdNotReady) : null;
                }
                else
                {
                    await view.SpinTo(IndexOf(slice), token);
                    Feedback.Success();
                    result = localizer.Get(EconomyKeys.WheelWon, EconomyText.Reward(localizer, Economy.Config, slice));
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || !IsOpen) return;
            Busy = false;
            view.SetBusy(false);
            Refresh();
        }

        private int IndexOf(WheelSlice slice)
        {
            for (var i = 0; i < slices.Count; i++)
                if (ReferenceEquals(slices[i], slice)) return i;
            return 0;
        }

        private void OnInfo()
        {
            if (!IsOpen) return;
            Feedback.Click();
            oddsShown = !oddsShown;
            view.ShowOdds(oddsShown ? Odds() : null);
        }
    }
}
