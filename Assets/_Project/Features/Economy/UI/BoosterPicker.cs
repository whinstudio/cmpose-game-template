using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // One row of the pre-run picker: the booster, how many are in stock, its price, whether it goes into the run and
    // whether a watched ad paid for it
    public readonly struct PickerRow
    {
        public PickerRow(string nameKey, string descriptionKey, Sprite icon, int owned, int price, bool selected, bool byAd)
        {
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            Icon = icon;
            Owned = owned;
            Price = price;
            Selected = selected;
            ByAd = byAd;
        }

        public string NameKey { get; }
        public string DescriptionKey { get; }
        // Null: the fallback circle with the name's first letter
        public Sprite Icon { get; }
        public int Owned { get; }
        public int Price { get; }
        public bool Selected { get; }
        public bool ByAd { get; }
    }

    public interface IBoosterPickerView
    {
        event Action<int> RowClicked;
        event Action PlayClicked;
        int MaxRows { get; }
        void SetOpen(bool open);
        // A price shows next to the currency's icon
        void ShowRows(IReadOnlyList<PickerRow> rows, EconomyCurrency currency);
    }

    public interface IBoosterPicker
    {
        // The chosen boosters' ids, each taken from the stock or paid with a watched ad (recorded in adUsed)
        UniTask<IReadOnlyList<string>> ChooseAsync(IReadOnlyList<BoosterConfig> boosters, ISet<string> adUsed, CancellationToken ct);
    }

    // The pre-run picker: a booster in stock is ticked by a tap; one not in stock opens its offer (coins add it to the
    // stock and tick it; a watched ad ticks it for this run only). Play takes the ticked ones out of the stock and
    // returns them; leaving (the scene, ct) returns none.
    public sealed class BoosterPicker : IBoosterPicker, IDisposable
    {
        private static readonly IReadOnlyList<string> None = Array.Empty<string>();

        private readonly IBoosterPickerView view;
        private readonly IEconomy economy;
        private readonly IBoosterOffer offer;
        private readonly EconomyCurrency currency;
        private readonly IUiFeedback feedback;
        private UniTaskCompletionSource<IReadOnlyList<string>> pending;
        private CancellationTokenRegistration cancelled;
        private CancellationToken token;
        private List<BoosterConfig> boosters = new List<BoosterConfig>();
        private bool[] selected = new bool[0];
        private bool[] byAd = new bool[0];
        private ISet<string> adUsed;
        private bool busy;

        public BoosterPicker(IBoosterPickerView view, IEconomy economy, IBoosterOffer offer, EconomyCurrency currency, IUiFeedback feedback)
        {
            this.currency = currency;
            this.view = view;
            this.economy = economy;
            this.offer = offer;
            this.feedback = feedback;
            view.RowClicked += OnRow;
            view.PlayClicked += OnPlay;
        }

        public bool IsOpen => pending != null;

        public UniTask<IReadOnlyList<string>> ChooseAsync(IReadOnlyList<BoosterConfig> offered, ISet<string> usedAds, CancellationToken ct)
        {
            if (IsOpen || offered == null || offered.Count == 0 || !economy.Enabled || ct.IsCancellationRequested)
                return UniTask.FromResult(None);
            pending = new UniTaskCompletionSource<IReadOnlyList<string>>();
            var task = pending.Task;
            boosters = new List<BoosterConfig>();
            for (var i = 0; i < offered.Count && i < view.MaxRows; i++) boosters.Add(offered[i]);
            selected = new bool[boosters.Count];
            byAd = new bool[boosters.Count];
            adUsed = usedAds ?? new HashSet<string>();
            token = ct;
            Show();
            view.SetOpen(true);
            cancelled = ct.Register(() => Finish(None));
            return task;
        }

        public void Dispose()
        {
            Finish(None);
            view.RowClicked -= OnRow;
            view.PlayClicked -= OnPlay;
        }

        private void Show()
        {
            var rows = new List<PickerRow>();
            for (var i = 0; i < boosters.Count; i++)
            {
                var booster = boosters[i];
                rows.Add(new PickerRow(booster.NameKey, booster.DescriptionKey, EconomyIcons.Booster(booster), economy.Count(booster.Id),
                    booster.Price, selected[i], byAd[i]));
            }
            view.ShowRows(rows, currency);
        }

        private void OnRow(int index)
        {
            if (!IsOpen || busy || index < 0 || index >= boosters.Count || byAd[index]) return;
            feedback.Click();
            if (selected[index] || economy.Count(boosters[index].Id) > 0)
            {
                selected[index] = !selected[index];
                Show();
                return;
            }
            BuyAsync(index).Forget();
        }

        private async UniTaskVoid BuyAsync(int index)
        {
            busy = true;
            var booster = boosters[index];
            OfferResult result;
            try
            {
                result = await offer.OfferAsync(booster, !adUsed.Contains(booster.Id), token);
            }
            finally
            {
                busy = false;
            }
            if (!IsOpen) return;
            if (result == OfferResult.Bought)
            {
                economy.Add(booster.Id, 1);
                selected[index] = true;
            }
            else if (result == OfferResult.Watched)
            {
                adUsed.Add(booster.Id);
                byAd[index] = true;
                selected[index] = true;
            }
            Show();
        }

        private void OnPlay()
        {
            if (!IsOpen || busy) return;
            feedback.Click();
            var chosen = new List<string>();
            for (var i = 0; i < boosters.Count; i++)
            {
                if (!selected[i]) continue;
                if (byAd[i] || economy.TryUse(boosters[i].Id)) chosen.Add(boosters[i].Id);
            }
            Finish(chosen);
        }

        private void Finish(IReadOnlyList<string> chosen)
        {
            if (!IsOpen) return;
            var done = pending;
            pending = null;
            busy = false;
            cancelled.Dispose();
            view.SetOpen(false);
            done.TrySetResult(chosen);
        }
    }
}
