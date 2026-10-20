using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // How an offer ended: closed, paid with coins (already spent), or an ad watched to the end
    public enum OfferResult
    {
        Closed,
        Bought,
        Watched
    }

    public interface IBoosterOfferView
    {
        event Action BuyClicked;
        event Action WatchClicked;
        event Action CloseClicked;
        void SetOpen(bool open);

        // icon null: the fallback circle with the name's first letter
        void ShowBooster(string nameKey, string descriptionKey, Sprite icon);

        // The price next to the currency's icon on the buy button, "You have: 20 acorns" under the description
        void ShowPrice(int price, int coins, EconomyCurrency currency);

        // The buy button is enabled while the coins reach the price; the ad button shows only while one can be watched
        void SetActions(bool canBuy, bool adShown);

        // A line under the buttons (null hides it)
        void ShowMessage(string key, params object[] args);

        void SetBusy(bool busy);
    }

    public interface IBoosterOffer
    {
        // Opens the offer of one booster and ends when the player pays with coins (Bought: spent), watches a rewarded
        // ad to the end (Watched) or closes it. adAllowed false hides the ad (this booster's ad of the run is used).
        // Closed at once while another offer is open, the economy is off or an AutoPlay run is on.
        UniTask<OfferResult> OfferAsync(BoosterConfig booster, bool adAllowed, CancellationToken ct);
    }

    // E4: the price is enabled while the currency reaches it; "Watch ad" shows while a rewarded ad is on offer (and
    // the run's ad of this booster is unused); with neither the window says "Acorns: not enough". An ad that is
    // not ready, closed early or failed gives nothing and says so; the window stays open.
    public sealed class BoosterOfferDialog : IBoosterOffer, IDisposable
    {
        private readonly IBoosterOfferView view;
        private readonly IEconomy economy;
        private readonly IEconomyAds ads;
        private readonly EconomyCurrency currency;
        private readonly IUiFeedback feedback;
        private readonly Func<bool> autoPlay;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private UniTaskCompletionSource<OfferResult> pending;
        private CancellationTokenRegistration cancelled;
        private BoosterConfig booster;
        private bool adAllowed;
        private bool adShown;
        private bool busy;
        private string message;

        public BoosterOfferDialog(IBoosterOfferView view, IEconomy economy, IEconomyAds ads, EconomyCurrency currency, IUiFeedback feedback,
            Func<bool> autoPlay)
        {
            this.view = view;
            this.economy = economy;
            this.ads = ads;
            this.currency = currency;
            this.feedback = feedback;
            this.autoPlay = autoPlay;
            view.BuyClicked += OnBuy;
            view.WatchClicked += OnWatch;
            view.CloseClicked += OnClose;
        }

        public bool IsOpen => pending != null;

        public UniTask<OfferResult> OfferAsync(BoosterConfig offered, bool allowAd, CancellationToken ct)
        {
            if (IsOpen || offered == null || !economy.Enabled || autoPlay() || ct.IsCancellationRequested)
                return UniTask.FromResult(OfferResult.Closed);
            pending = new UniTaskCompletionSource<OfferResult>();
            var task = pending.Task;
            booster = offered;
            adAllowed = allowAd;
            message = null;
            view.ShowBooster(booster.NameKey, booster.DescriptionKey, EconomyIcons.Booster(booster));
            view.ShowMessage(null);
            view.SetBusy(false);
            Refresh();
            view.SetOpen(true);
            economy.Changed += Refresh;
            cancelled = ct.Register(() => Finish(OfferResult.Closed));
            return task;
        }

        public void Dispose()
        {
            Finish(OfferResult.Closed);
            lifetime.Cancel();
            lifetime.Dispose();
            view.BuyClicked -= OnBuy;
            view.WatchClicked -= OnWatch;
            view.CloseClicked -= OnClose;
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            var coins = economy.Coins;
            var canBuy = coins >= booster.Price;
            adShown = adAllowed && ads.RewardedAvailable;
            const string notEnough = EconomyKeys.CurrencyNotEnough;
            view.ShowPrice(booster.Price, coins, currency);
            view.SetActions(canBuy, adShown);
            if (!canBuy && !adShown) Say(notEnough);
            else if (message == notEnough) Say(null);
        }

        private void Say(string key)
        {
            message = key;
            if (key == EconomyKeys.CurrencyNotEnough) view.ShowMessage(key, currency.CapitalNameArg);
            else view.ShowMessage(key);
        }

        private void OnBuy()
        {
            if (!IsOpen || busy) return;
            if (!economy.TrySpend(booster.Price))
            {
                Refresh();
                return;
            }
            feedback.Click();
            Finish(OfferResult.Bought);
        }

        private void OnWatch()
        {
            if (!IsOpen || busy || !adShown) return;
            feedback.Click();
            WatchAsync().Forget();
        }

        private async UniTaskVoid WatchAsync()
        {
            busy = true;
            view.SetBusy(true);
            bool earned;
            try
            {
                earned = await ads.ShowRewardedAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                earned = false;
            }
            finally
            {
                busy = false;
            }
            if (!IsOpen) return;
            view.SetBusy(false);
            if (earned)
            {
                Finish(OfferResult.Watched);
                return;
            }
            Refresh();
            Say(EconomyKeys.AdNotReady);
        }

        private void OnClose()
        {
            if (!IsOpen || busy) return;
            feedback.Click();
            Finish(OfferResult.Closed);
        }

        private void Finish(OfferResult result)
        {
            if (!IsOpen) return;
            var done = pending;
            pending = null;
            booster = null;
            busy = false;
            economy.Changed -= Refresh;
            cancelled.Dispose();
            view.SetOpen(false);
            done.TrySetResult(result);
        }
    }
}
