using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Economy;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // The shop's states: loading, rows (the catalog's rows the offering has, at the store's prices), failed (Try again),
    // busy while a purchase or restore runs (every press is ignored then, Close too). A purchase keeps the shop open
    // (coin packs may be bought again; a bought one-time row leaves); Cancelled says nothing; Error shows its text. The
    // shop closes once nothing is left to sell. Rewrite the view for your game; keep this flow.
    public sealed class ShopPresenter : IDisposable
    {
        // The coin packs' grid: three columns for the full shop's six, two for the simple shop's four
        public const int FullColumns = 3;
        public const int SimpleColumns = 2;

        private readonly IShopView view;
        private readonly IPurchases purchases;
        private readonly IShopCatalog catalog;
        private readonly IEconomy economy;
        private readonly EconomyCurrency currency;
        private readonly ILocalizer localizer;
        private readonly bool adsOn;
        private readonly IUiFeedback feedback;
        private readonly ILog log;
        private readonly Func<bool> autoPlay;
        private readonly HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<ShopProduct> products;
        private CancellationTokenSource loading;
        private ShopSource source;
        private bool busy;
        private bool disposed;

        public ShopPresenter(IShopView view, IPurchases purchases, IShopCatalog catalog, IEconomy economy, EconomyCurrency currency,
            ILocalizer localizer, bool adsOn, IUiFeedback feedback, ILog log, Func<bool> autoPlay)
        {
            this.view = view;
            this.purchases = purchases;
            this.catalog = catalog;
            this.economy = economy;
            this.currency = currency;
            this.localizer = localizer;
            this.adsOn = adsOn;
            this.feedback = feedback;
            this.log = log;
            this.autoPlay = autoPlay;
        }

        public bool IsOpen { get; private set; }

        // The rows the shop may sell now, in the catalog's order: none that turns the ads off in a game without ads or
        // for a player with premium, and no one-time row the player owns. Without a catalog: Remove Ads only.
        public static IReadOnlyList<ShopRowContent> Lineup(IShopCatalog catalog, IPurchases purchases, bool adsOn)
        {
            var rows = catalog != null
                ? catalog.Rows
                : new[] { new ShopRowContent(ShopProducts.RemoveAds, 0, new Dictionary<string, int>(), null) };
            var lineup = new List<ShopRowContent>();
            foreach (var row in rows)
            {
                if (ShopProducts.GrantsPremium(row.Id) && (!adsOn || purchases.IsPremium)) continue;
                if (ShopProducts.IsOneTime(row.Id) && purchases.Owns(row.Id)) continue;
                lineup.Add(row);
            }
            return lineup;
        }

        public static bool CanShow(IShopCatalog catalog, IPurchases purchases, bool adsOn) =>
            purchases.IsAvailable && Lineup(catalog, purchases, adsOn).Count > 0;

        public void Start()
        {
            view.CloseClicked += OnClose;
            view.RestoreClicked += OnRestore;
            view.RetryClicked += OnRetry;
            view.BuyClicked += OnBuy;
            purchases.Changed += OnPurchasesChanged;
            economy.Changed += ShowWallet;
        }

        public void Dispose()
        {
            disposed = true;
            CancelLoading();
            view.CloseClicked -= OnClose;
            view.RestoreClicked -= OnRestore;
            view.RetryClicked -= OnRetry;
            view.BuyClicked -= OnBuy;
            purchases.Changed -= OnPurchasesChanged;
            economy.Changed -= ShowWallet;
        }

        // Nothing opens during an AutoPlay run: a shop over the game would stall it
        public void Open(ShopSource from)
        {
            if (IsOpen || disposed || autoPlay()) return;
            IsOpen = true;
            source = from;
            view.SetOpen(true);
            ShowWallet();
            LoadAsync().Forget();
        }

        private async UniTaskVoid LoadAsync()
        {
            CancelLoading();
            loading = new CancellationTokenSource();
            var token = loading.Token;
            products = null;
            shown.Clear();
            view.ShowMessage(null);
            view.ShowLoading();
            IReadOnlyList<ShopProduct> loaded;
            try
            {
                loaded = await purchases.ProductsAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || disposed) return;
            products = loaded;
            ShowRows();
            log.Marker($"CMPOSE_SHOP shown={source.ToString().ToLowerInvariant()} rows={shown.Count}");
        }

        // The lineup's rows the offering has, at its prices; a row the store does not sell (not set up yet) is left out.
        // No offering, or no row left in it: the error and Try again.
        private void ShowRows()
        {
            shown.Clear();
            if (products == null)
            {
                view.ShowFailed();
                return;
            }
            var prices = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var product in products)
            {
                if (product != null && product.Id != null && !prices.ContainsKey(product.Id)) prices[product.Id] = product.PriceString;
            }
            var rows = new List<ShopRow>();
            var packs = new List<ShopRow>();
            foreach (var content in Lineup(catalog, purchases, adsOn))
            {
                if (!prices.TryGetValue(content.Id, out var price)) continue;
                var pack = ShopProducts.IsCoinPack(content.Id);
                var list = pack ? packs : rows;
                if (list.Count >= (pack ? view.MaxPacks : view.MaxRows)) continue;
                list.Add(pack
                    ? new ShopRow(content.Id, null, currency.Amount(content.Coins), price, content.Tag)
                    : new ShopRow(content.Id, PurchasesKeys.RowTitle(content.Id), Describe(content), price, content.Tag));
                shown.Add(content.Id);
            }
            if (shown.Count == 0) view.ShowFailed();
            else view.ShowRows(rows, packs, PackColumns);
        }

        // "No ads, +400 coins, +2 Bomb": what the row turns off, then what it pays, in the bonus line's form
        private string Describe(ShopRowContent content)
        {
            var parts = new List<string>();
            if (ShopProducts.GrantsPremium(content.Id)) parts.Add(localizer.Get(PurchasesKeys.NoAds));
            if (content.Coins > 0) parts.Add("+" + currency.Amount(content.Coins));
            foreach (var booster in content.Boosters)
            {
                var name = EconomyText.BoosterName(localizer, economy.Config, booster.Key);
                parts.Add("+" + localizer.Get(EconomyKeys.RewardBooster, booster.Value, name));
            }
            return string.Join(", ", parts);
        }

        private int PackColumns => economy.Config != null && economy.Config.Shop.Kind == ShopConfig.Full ? FullColumns : SimpleColumns;

        private void ShowWallet()
        {
            if (!IsOpen) return;
            view.ShowWallet(economy.Enabled ? economy.Coins : (int?)null, currency.Mark);
        }

        private void OnBuy(string id)
        {
            if (busy || id == null || !shown.Contains(id)) return;
            feedback.Click();
            RunAsync(id).Forget();
        }

        private void OnRestore()
        {
            if (busy) return;
            feedback.Click();
            RunAsync(null).Forget();
        }

        private void OnRetry()
        {
            if (!IsOpen || busy) return;
            feedback.Click();
            LoadAsync().Forget();
        }

        // productId null: a restore
        private async UniTaskVoid RunAsync(string productId)
        {
            var purchase = productId != null;
            busy = true;
            view.SetBusy(true);
            view.ShowMessage(null);
            var result = purchase
                ? await purchases.PurchaseAsync(productId, CancellationToken.None)
                : await purchases.RestoreAsync(CancellationToken.None);
            busy = false;
            if (disposed) return;
            view.SetBusy(false);
            log.Marker($"CMPOSE_{(purchase ? "PURCHASE" : "RESTORE")} result={result.Outcome}");
            if (result.Outcome == PurchaseOutcome.Error)
            {
                view.ShowMessage(result.MessageKey);
                return;
            }
            if (result.Outcome != PurchaseOutcome.Success || !IsOpen) return;
            if (Lineup(catalog, purchases, adsOn).Count == 0)
            {
                Close();
                return;
            }
            if (products != null) ShowRows();
            if (!purchase) view.ShowMessage(PurchasesKeys.Restored);
        }

        // Premium arrived (a refresh, a restore elsewhere): its rows leave
        private void OnPurchasesChanged()
        {
            if (!IsOpen || busy || products == null) return;
            if (Lineup(catalog, purchases, adsOn).Count == 0) Close();
            else ShowRows();
        }

        private void OnClose()
        {
            if (busy) return;
            feedback.Click();
            Close();
        }

        private void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            CancelLoading();
            view.SetOpen(false);
        }

        private void CancelLoading()
        {
            if (loading == null) return;
            loading.Cancel();
            loading.Dispose();
            loading = null;
        }
    }
}
