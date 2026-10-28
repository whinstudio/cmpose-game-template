using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Features.Shell.Purchases;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class FakeShopView : IShopView
    {
        public readonly List<string> Calls = new List<string>();
        public IReadOnlyList<ShopRow> Rows = new ShopRow[0];
        public IReadOnlyList<ShopRow> Packs = new ShopRow[0];
        public int Columns;
        public bool Open;
        public bool Busy;
        public bool Failed;
        public string Message;
        public int? Coins;
        public event Action CloseClicked;
        public event Action RestoreClicked;
        public event Action RetryClicked;
        public event Action<string> BuyClicked;
        public int MaxRows => 10;
        public int MaxPacks => 6;
        public void SetOpen(bool open) { Open = open; Calls.Add(open ? "open" : "closed"); }
        public void ShowWallet(int? coins, CurrencyMark mark) => Coins = coins;
        public void ShowLoading() { Failed = false; Calls.Add("loading"); }

        public void ShowRows(IReadOnlyList<ShopRow> rows, IReadOnlyList<ShopRow> packs, int packColumns)
        {
            Rows = rows;
            Packs = packs;
            Columns = packColumns;
            Failed = false;
            Calls.Add($"rows {rows.Count}+{packs.Count}");
        }

        public void ShowFailed() { Failed = true; Calls.Add("failed"); }
        public void ShowMessage(string key) => Message = key;
        public void SetBusy(bool busy) => Busy = busy;
        public void ClickClose() => CloseClicked?.Invoke();
        public void ClickRestore() => RestoreClicked?.Invoke();
        public void ClickRetry() => RetryClicked?.Invoke();
        public void ClickBuy(string id) => BuyClicked?.Invoke(id);
        public IEnumerable<string> Ids => Rows.Select(r => r.Id).Concat(Packs.Select(p => p.Id));
    }

    // The shop's rows come from the catalog, in its order, at the offering's prices; ownership, premium and a game
    // without ads take rows away; a failed offering offers Try again
    public sealed class ShopPresenterTests
    {
        private const string FullJson =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}]," +
            "\"shop\":{\"kind\":\"full\",\"rows\":[" +
            "{\"id\":\"starter_pack\",\"coins\":400,\"boosters\":{\"bomb\":2},\"tag\":90}," +
            "{\"id\":\"remove_ads\"}," +
            "{\"id\":\"no_ads_bundle\",\"coins\":500,\"boosters\":{\"bomb\":3}}," +
            "{\"id\":\"bundle_1\",\"coins\":1000}," +
            "{\"id\":\"coins_1\",\"coins\":100}," +
            "{\"id\":\"coins_2\",\"coins\":300}]}}";

        private const string SimpleJson =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}]," +
            "\"shop\":{\"kind\":\"simple\",\"rows\":[{\"id\":\"remove_ads\"},{\"id\":\"coins_1\",\"coins\":100}]}}";

        // The offering in the dashboard's order, which is not the catalog's
        private static readonly IReadOnlyList<ShopProduct> Offering = new[]
        {
            new ShopProduct("coins_2", "$1.99"),
            new ShopProduct("coins_1", "$0.99"),
            new ShopProduct("bundle_1", "$9.99"),
            new ShopProduct(ShopProducts.NoAdsBundle, "$9.99"),
            new ShopProduct(ShopProducts.RemoveAds, "$4.99"),
            new ShopProduct(ShopProducts.StarterPack, "$1.99")
        };

        private static readonly string[] FullOrder =
            { ShopProducts.StarterPack, ShopProducts.RemoveAds, ShopProducts.NoAdsBundle, "bundle_1", "coins_1", "coins_2" };

        private sealed class Rig
        {
            public readonly FakeShopView View = new FakeShopView();
            public readonly FakePurchases Purchases = new FakePurchases { Products = Offering };
            public readonly FakeLog Log = new FakeLog();
            public readonly FakeUiFeedback Feedback = new FakeUiFeedback();
            public readonly Economy Economy;
            public readonly IShopCatalog Catalog;
            public readonly ShopPresenter Presenter;
            public bool AutoPlay;

            // json null: no economy (and no catalog)
            public Rig(string json = FullJson, bool adsOn = true)
            {
                var config = json == null ? null : EconomyConfig.Parse(json);
                Economy = new Economy(config, new MemoryEconomyStore());
                Catalog = json == null ? null : new EconomyShopCatalog(config);
                var localizer = new FakeLocalizer();
                Presenter = new ShopPresenter(View, Purchases, Catalog, Economy, new EconomyCurrency(config, localizer), localizer, adsOn,
                    Feedback, Log, () => AutoPlay);
                Presenter.Start();
            }

            public bool CanShow => ShopPresenter.CanShow(Catalog, Purchases, true);
        }

        private static StoreTransaction Bought(string productId) => FakeRevenueCatGateway.Transaction("t-" + productId, productId);

        [Test]
        public void full_shop_shows_rows_in_catalog_order_with_store_prices()
        {
            var rig = new Rig();

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Calls, Is.EqualTo(new[] { "open", "loading", "rows 4+2" }));
            Assert.That(rig.View.Ids, Is.EqualTo(FullOrder));
            Assert.That(rig.View.Rows.Select(r => r.Price), Is.EqualTo(new[] { "$1.99", "$4.99", "$9.99", "$9.99" }));
            Assert.That(rig.View.Packs.Select(p => p.Price), Is.EqualTo(new[] { "$0.99", "$1.99" }));
            Assert.That(rig.View.Rows[0].TitleKey, Is.EqualTo("purchases.shop.starter_pack.title"));
            Assert.That(rig.View.Rows[0].Tag, Is.EqualTo(90));
            Assert.That(rig.View.Rows[0].Content, Does.StartWith("+economy.currency.amount(400,coins), +economy.reward.booster(2,"));
            Assert.That(rig.View.Rows[1].Content, Is.EqualTo(PurchasesKeys.NoAds));
            Assert.That(rig.View.Rows[2].Content, Does.StartWith(PurchasesKeys.NoAds + ", +economy.currency.amount(500,coins)"));
            Assert.That(rig.View.Packs[0].TitleKey, Is.Null);
            Assert.That(rig.View.Packs[0].Content, Is.EqualTo("economy.currency.amount(100,coins)"));
            Assert.That(rig.View.Columns, Is.EqualTo(ShopPresenter.FullColumns));
            Assert.That(rig.View.Coins, Is.EqualTo(0));
            Assert.That(rig.Log.Lines, Has.Some.EqualTo("MARKER CMPOSE_SHOP shown=menu rows=6"));
        }

        [Test]
        public void a_simple_shop_lays_its_packs_in_two_columns()
        {
            var rig = new Rig(SimpleJson);

            rig.Presenter.Open(ShopSource.Currency);

            Assert.That(rig.View.Ids, Is.EqualTo(new[] { ShopProducts.RemoveAds, "coins_1" }));
            Assert.That(rig.View.Columns, Is.EqualTo(ShopPresenter.SimpleColumns));
        }

        [Test]
        public void a_row_missing_from_the_offering_is_hidden()
        {
            var rig = new Rig();
            rig.Purchases.Products = Offering.Where(p => p.Id != "bundle_1" && p.Id != "coins_2").ToList();

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Ids, Is.EqualTo(new[] { ShopProducts.StarterPack, ShopProducts.RemoveAds, ShopProducts.NoAdsBundle, "coins_1" }));
            Assert.That(rig.View.Failed, Is.False);
        }

        [Test]
        public void premium_hides_remove_ads_and_the_no_ads_bundle()
        {
            var rig = new Rig();
            rig.Purchases.IsPremium = true;

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Ids, Is.EqualTo(new[] { ShopProducts.StarterPack, "bundle_1", "coins_1", "coins_2" }));
        }

        [Test]
        public void premium_arriving_while_open_takes_its_rows_away()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);

            rig.Purchases.IsPremium = true;
            rig.Purchases.RaiseChanged();

            Assert.That(rig.View.Ids, Does.Not.Contain(ShopProducts.RemoveAds).And.Not.Contain(ShopProducts.NoAdsBundle));
            Assert.That(rig.View.Open, Is.True);
        }

        [Test]
        public void an_owned_starter_pack_is_hidden()
        {
            var rig = new Rig();
            rig.Purchases.Transactions = new[] { Bought(ShopProducts.StarterPack), Bought("coins_1") };

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Ids, Is.EqualTo(FullOrder.Skip(1)), "a bought coin pack stays: it sells again");
        }

        [Test]
        public void without_a_catalog_only_remove_ads_is_shown()
        {
            var rig = new Rig(json: null);

            rig.Presenter.Open(ShopSource.Settings);

            Assert.That(rig.View.Ids, Is.EqualTo(new[] { ShopProducts.RemoveAds }));
            Assert.That(rig.View.Coins, Is.Null, "no economy: no wallet");
            Assert.That(rig.CanShow, Is.True);
        }

        [Test]
        public void without_ads_remove_ads_rows_are_hidden()
        {
            var rig = new Rig(adsOn: false);

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Ids, Is.EqualTo(new[] { ShopProducts.StarterPack, "bundle_1", "coins_1", "coins_2" }));
        }

        [Test]
        public void no_ads_and_no_economy_leave_no_shop()
        {
            var purchases = new FakePurchases();

            Assert.That(ShopPresenter.CanShow(null, purchases, false), Is.False);
            Assert.That(ShopPresenter.CanShow(null, purchases, true), Is.True);
            purchases.IsPremium = true;
            Assert.That(ShopPresenter.CanShow(null, purchases, true), Is.False, "Remove Ads is on: nothing left to sell");
            purchases.IsPremium = false;
            purchases.IsAvailable = false;
            Assert.That(ShopPresenter.CanShow(null, purchases, true), Is.False);
        }

        [Test]
        public void a_failed_offering_shows_retry()
        {
            var rig = new Rig();
            rig.Purchases.Products = null;

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Calls, Is.EqualTo(new[] { "open", "loading", "failed" }));
            Assert.That(rig.View.Open, Is.True);
            Assert.That(rig.Log.Lines, Has.Some.EqualTo("MARKER CMPOSE_SHOP shown=menu rows=0"));
            rig.View.ClickBuy(ShopProducts.RemoveAds);
            Assert.That(rig.Purchases.Purchased, Is.Empty, "no row: nothing to buy");
        }

        [Test]
        public void an_offering_without_any_of_the_rows_shows_retry()
        {
            var rig = new Rig();
            rig.Purchases.Products = new[] { new ShopProduct("weekly", "$2.99") };

            rig.Presenter.Open(ShopSource.Menu);

            Assert.That(rig.View.Failed, Is.True);
        }

        [Test]
        public void retry_loads_again()
        {
            var rig = new Rig();
            rig.Purchases.Products = null;
            rig.Presenter.Open(ShopSource.Menu);

            rig.Purchases.Products = Offering;
            rig.View.ClickRetry();

            Assert.That(rig.Purchases.ProductCalls, Is.EqualTo(2));
            Assert.That(rig.View.Calls, Is.EqualTo(new[] { "open", "loading", "failed", "loading", "rows 4+2" }));
            Assert.That(rig.Feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void a_coin_pack_purchase_keeps_the_shop_open_and_sells_it_again()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);

            rig.View.ClickBuy("coins_2");
            rig.Purchases.Transactions = new[] { Bought("coins_2") };

            Assert.That(rig.Purchases.Purchased, Is.EqualTo(new[] { "coins_2" }));
            Assert.That(rig.View.Open, Is.True);
            Assert.That(rig.View.Ids, Has.Member("coins_2"));
            Assert.That(rig.Log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PURCHASE result=Success"));
        }

        [Test]
        public void a_bought_one_time_row_leaves_the_shop()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);
            rig.Purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();

            rig.View.ClickBuy(ShopProducts.StarterPack);
            rig.Purchases.Transactions = new[] { Bought(ShopProducts.StarterPack) };
            rig.Purchases.PendingPurchase.TrySetResult(PurchaseResult.Success);

            Assert.That(rig.View.Ids, Is.EqualTo(FullOrder.Skip(1)));
        }

        [Test]
        public void buying_the_last_row_closes_the_shop()
        {
            var rig = new Rig(json: null);
            rig.Presenter.Open(ShopSource.Settings);
            rig.Purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();

            rig.View.ClickBuy(ShopProducts.RemoveAds);
            rig.Purchases.IsPremium = true;
            rig.Purchases.PendingPurchase.TrySetResult(PurchaseResult.Success);

            Assert.That(rig.View.Open, Is.False);
            Assert.That(rig.Presenter.IsOpen, Is.False);
        }

        [Test]
        public void a_row_not_shown_cannot_be_bought()
        {
            var rig = new Rig();
            rig.Purchases.IsPremium = true;
            rig.Presenter.Open(ShopSource.Menu);

            rig.View.ClickBuy(ShopProducts.RemoveAds);
            rig.View.ClickBuy("coins_9");
            rig.View.ClickBuy(null);

            Assert.That(rig.Purchases.Purchased, Is.Empty);
        }

        [Test]
        public void cancelled_stays_silent_and_an_error_shows_its_text()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);

            rig.Purchases.PurchaseResult = PurchaseResult.Cancelled;
            rig.View.ClickBuy("coins_1");
            Assert.That(rig.View.Message, Is.Null);

            rig.Purchases.PurchaseResult = PurchaseResult.Error("purchases.error.network");
            rig.View.ClickBuy("coins_1");
            Assert.That(rig.View.Message, Is.EqualTo("purchases.error.network"));
            Assert.That(rig.View.Open, Is.True);
            Assert.That(rig.Log.Lines.Where(l => l.StartsWith("MARKER CMPOSE_PURCHASE ")), Is.EqualTo(new[]
            {
                "MARKER CMPOSE_PURCHASE result=Cancelled", "MARKER CMPOSE_PURCHASE result=Error"
            }));
        }

        [Test]
        public void restore_success_and_nothing_to_restore_messages()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Settings);

            rig.Purchases.RestoreResult = PurchaseResult.Error(PurchasesKeys.ErrorNothingToRestore);
            rig.View.ClickRestore();
            Assert.That(rig.View.Message, Is.EqualTo(PurchasesKeys.ErrorNothingToRestore));

            rig.Purchases.RestoreResult = PurchaseResult.Success;
            rig.Purchases.IsPremium = true;
            rig.View.ClickRestore();
            Assert.That(rig.View.Message, Is.EqualTo(PurchasesKeys.Restored));
            Assert.That(rig.View.Open, Is.True);
            Assert.That(rig.View.Ids, Does.Not.Contain(ShopProducts.RemoveAds));
            Assert.That(rig.Purchases.Restores, Is.EqualTo(2));
            Assert.That(rig.Log.Lines, Has.Some.EqualTo("MARKER CMPOSE_RESTORE result=Success"));
        }

        [Test]
        public void does_not_open_while_autoplay_is_requested()
        {
            var rig = new Rig { AutoPlay = true };

            rig.Presenter.Open(ShopSource.NotEnough);

            Assert.That(rig.Presenter.IsOpen, Is.False);
            Assert.That(rig.View.Calls, Is.Empty);
            Assert.That(rig.Purchases.ProductCalls, Is.Zero);
        }

        [Test]
        public void presses_while_busy_are_ignored()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);
            rig.Purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();

            rig.View.ClickBuy("coins_1");
            Assert.That(rig.View.Busy, Is.True);
            rig.View.ClickBuy("coins_2");
            rig.View.ClickRestore();
            rig.View.ClickRetry();
            rig.View.ClickClose();
            Assert.That(rig.Purchases.Purchased.Count, Is.EqualTo(1));
            Assert.That(rig.Purchases.Restores, Is.Zero);
            Assert.That(rig.Purchases.ProductCalls, Is.EqualTo(1));
            Assert.That(rig.View.Open, Is.True);

            rig.Purchases.PendingPurchase.TrySetResult(PurchaseResult.Success);
            Assert.That(rig.View.Busy, Is.False);
        }

        [Test]
        public void the_wallet_follows_the_coins_while_open()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Currency);

            rig.Economy.Earn(300);

            Assert.That(rig.View.Coins, Is.EqualTo(300));
        }

        [Test]
        public void late_products_after_close_change_nothing_and_reopening_loads_again()
        {
            var rig = new Rig();
            rig.Purchases.PendingProducts = new UniTaskCompletionSource<IReadOnlyList<ShopProduct>>();
            rig.Presenter.Open(ShopSource.Menu);
            var token = rig.Purchases.LastProductsToken;

            rig.View.ClickClose();
            Assert.That(token.IsCancellationRequested, Is.True);
            rig.Purchases.PendingProducts.TrySetResult(Offering);

            Assert.That(rig.View.Calls, Is.EqualTo(new[] { "open", "loading", "closed" }));
            Assert.That(rig.Log.Lines.Any(l => l.Contains("CMPOSE_SHOP")), Is.False);

            rig.Purchases.PendingProducts = null;
            rig.Presenter.Open(ShopSource.Settings);
            Assert.That(rig.Purchases.ProductCalls, Is.EqualTo(2));
            Assert.That(rig.View.Calls.Last(), Is.EqualTo("rows 4+2"));
        }

        [Test]
        public void opening_an_open_shop_does_nothing()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);
            rig.Presenter.Open(ShopSource.Settings);

            Assert.That(rig.Purchases.ProductCalls, Is.EqualTo(1));
        }

        [Test]
        public void dispose_cancels_and_a_late_result_changes_nothing()
        {
            var rig = new Rig();
            rig.Presenter.Open(ShopSource.Menu);
            rig.Purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();
            rig.View.ClickBuy("coins_1");

            rig.Presenter.Dispose();
            rig.Purchases.PendingPurchase.TrySetResult(PurchaseResult.Error("purchases.error.failed"));
            rig.View.ClickBuy("coins_1");

            Assert.That(rig.View.Message, Is.Null);
            Assert.That(rig.Purchases.Purchased.Count, Is.EqualTo(1));
            Assert.That(rig.Log.Lines.Any(l => l.Contains("CMPOSE_PURCHASE ")), Is.False);
        }
    }
}
