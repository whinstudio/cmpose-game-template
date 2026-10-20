using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    public enum SupplyRowState
    {
        Locked,
        Ready,
        Claimed
    }

    // One ad row of the supply ladder
    public readonly struct SupplyRow
    {
        public SupplyRow(int coins, SupplyRowState state)
        {
            Coins = coins;
            State = state;
        }

        public int Coins { get; }
        public SupplyRowState State { get; }
    }

    public interface ISupplyWindowView
    {
        event Action FreeClicked;
        event Action<int> AdClicked;
        event Action CloseClicked;
        int MaxAdRows { get; }
        void SetOpen(bool open);

        // The free row: its coins, Claim while ready, else "Ready in <timer>"
        void ShowFree(int coins, bool ready, string timer);

        // The ad rows in order (an empty list hides them and their header)
        void ShowAdRows(IReadOnlyList<SupplyRow> rows);

        // Every row's currency icon (its glyph or fallback disc, in its colour)
        void ShowCurrency(CurrencyMark mark);

        // A line under the rows (null hides it)
        void ShowMessage(string key);

        void SetBusy(bool busy);
    }

    // The daily supply window (E5): the timed free row on top, the ad rows under it unlocking in order (the next one
    // can be watched while a rewarded ad is on offer). The ad rows hide while no ad can be offered and none was claimed
    // today (ads off, the daily cap reached).
    public sealed class SupplyWindow : EconomyWindow
    {
        private readonly ISupplyWindowView view;
        private readonly Supply supply;
        private readonly EconomyCurrency currency;

        public SupplyWindow(ISupplyWindowView view, Economy economy, Supply supply, IEconomyAds ads, EconomyCurrency currency,
            IUiFeedback feedback, Func<bool> autoPlay) : base(economy, ads, feedback, autoPlay)
        {
            this.view = view;
            this.supply = supply;
            this.currency = currency;
            view.FreeClicked += OnFree;
            view.AdClicked += OnAd;
            view.CloseClicked += OnCloseClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            view.FreeClicked -= OnFree;
            view.AdClicked -= OnAd;
            view.CloseClicked -= OnCloseClicked;
        }

        protected override bool Available => supply.Configured;

        protected override void OnOpen()
        {
            view.ShowCurrency(currency.Mark);
            view.ShowMessage(null);
            view.SetBusy(false);
        }

        protected override void SetViewOpen(bool open) => view.SetOpen(open);

        protected override void Refresh()
        {
            var config = Economy.Config.Supply;
            view.ShowFree(config.FreeCoins, supply.FreeReady, EconomyText.Timer(supply.FreeReadyIn));
            var claimed = supply.AdRowsClaimed;
            var rows = new List<SupplyRow>();
            if (Ads.RewardedAvailable || claimed > 0)
            {
                var next = supply.AdRowReady ? supply.NextAdRow : -1;
                for (var i = 0; i < config.AdCoins.Count && i < view.MaxAdRows; i++)
                {
                    var state = i < claimed ? SupplyRowState.Claimed : i == next ? SupplyRowState.Ready : SupplyRowState.Locked;
                    rows.Add(new SupplyRow(config.AdCoins[i], state));
                }
            }
            view.ShowAdRows(rows);
        }

        private void OnFree()
        {
            if (!IsOpen || Busy) return;
            if (supply.ClaimFree()) Feedback.Success();
            else Feedback.Click();
            Refresh();
        }

        private void OnAd(int row)
        {
            if (!IsOpen || Busy) return;
            Feedback.Click();
            ClaimAsync(row).Forget();
        }

        private async UniTaskVoid ClaimAsync(int row)
        {
            Busy = true;
            view.SetBusy(true);
            view.ShowMessage(null);
            var token = Token;
            bool paid;
            try
            {
                paid = await supply.ClaimAdRowAsync(row, token);
            }
            catch (OperationCanceledException)
            {
                paid = false;
            }
            if (token.IsCancellationRequested || !IsOpen) return;
            Busy = false;
            view.SetBusy(false);
            if (paid) Feedback.Success();
            else view.ShowMessage(EconomyKeys.AdNotReady);
            Refresh();
        }
    }
}
