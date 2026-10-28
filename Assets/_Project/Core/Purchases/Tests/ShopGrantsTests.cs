using System;
using Kturker.UnityBase.Features.Economy;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    // The shop's ledger: a bought row pays once per store transaction, whether it arrives by the purchase event, by a
    // customer info or by both; history from before this install's first launch pays nothing
    public sealed class ShopGrantsTests
    {
        private const string Json =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}," +
            "{\"id\":\"shield\",\"price\":120,\"offer\":\"preRun\",\"start\":0}]," +
            "\"shop\":{\"kind\":\"full\",\"rows\":[" +
            "{\"id\":\"starter_pack\",\"usd\":1.99,\"coins\":400,\"boosters\":{\"bomb\":2},\"tag\":90}," +
            "{\"id\":\"remove_ads\",\"usd\":4.99}," +
            "{\"id\":\"bundle_1\",\"usd\":9.99,\"coins\":1000,\"boosters\":{\"bomb\":1,\"shield\":1}}," +
            "{\"id\":\"coins_2\",\"usd\":1.99,\"coins\":300}]}}";

        // FakeRevenueCatGateway.Transaction buys at 2026-10-28 12:00 UTC; the game was first launched two hours before
        private static readonly DateTime FirstLaunch = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);

        private sealed class Rig
        {
            public readonly MemoryEconomyStore Store = new MemoryEconomyStore();
            public readonly FakePurchases Purchases = new FakePurchases();
            public readonly Economy Economy;
            public readonly ShopGrants Grants;
            public DateTime Now = FirstLaunch;

            public Rig(string json = Json, bool launched = true)
            {
                if (launched) Store.Save(new EconomyState { FirstLaunchUtcTicks = FirstLaunch.Ticks });
                var config = json == null ? null : EconomyConfig.Parse(json);
                Economy = new Economy(config, Store);
                Grants = new ShopGrants(Economy, Purchases, new EconomyShopCatalog(config), () => Now);
                Grants.Initialize();
            }

            public EconomyState State => Store.Load();
        }

        [Test]
        public void a_coin_pack_purchase_grants_its_coins()
        {
            var rig = new Rig();
            rig.Purchases.RaiseProductPurchased(FakeRevenueCatGateway.Transaction("t1", "coins_2"));

            Assert.That(rig.Economy.Coins, Is.EqualTo(300));
            Assert.That(rig.State.GrantedTransactions, Is.EqualTo(new[] { "t1" }));
        }

        [Test]
        public void the_same_transaction_twice_grants_once()
        {
            var rig = new Rig();
            var transaction = FakeRevenueCatGateway.Transaction("t1", "coins_2");
            rig.Purchases.RaiseProductPurchased(transaction);
            rig.Purchases.RaiseTransactionsUpdated(new[] { transaction });
            rig.Purchases.RaiseTransactionsUpdated(new[] { FakeRevenueCatGateway.Transaction("t1", "coins_2") });

            Assert.That(rig.Economy.Coins, Is.EqualTo(300));
            Assert.That(rig.State.GrantedTransactions, Is.EqualTo(new[] { "t1" }));
        }

        [Test]
        public void a_purchase_missed_by_the_event_is_granted_from_the_next_customer_info()
        {
            var rig = new Rig();
            rig.Purchases.RaiseTransactionsUpdated(new[] { FakeRevenueCatGateway.Transaction("t2", "bundle_1") });

            Assert.That(rig.Economy.Coins, Is.EqualTo(1000));
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(1));
            Assert.That(rig.Economy.Count("shield"), Is.EqualTo(1));
            Assert.That(rig.State.GrantedTransactions, Is.EqualTo(new[] { "t2" }));
        }

        [Test]
        public void a_purchase_the_store_knew_at_launch_is_granted_on_initialize()
        {
            var store = new MemoryEconomyStore();
            store.Save(new EconomyState { FirstLaunchUtcTicks = FirstLaunch.Ticks });
            var config = EconomyConfig.Parse(Json);
            var economy = new Economy(config, store);
            var purchases = new FakePurchases { Transactions = new[] { FakeRevenueCatGateway.Transaction("t3", "coins_2") } };
            var grants = new ShopGrants(economy, purchases, new EconomyShopCatalog(config), () => FirstLaunch);

            grants.Initialize();

            Assert.That(economy.Coins, Is.EqualTo(300));
            grants.Dispose();
        }

        [Test]
        public void transactions_before_the_first_launch_grant_nothing()
        {
            var rig = new Rig();
            var before = new StoreTransaction("t0", "coins_2", FirstLaunch.AddDays(-3));
            rig.Purchases.RaiseTransactionsUpdated(new[] { before });

            Assert.That(rig.Economy.Coins, Is.EqualTo(0));
            Assert.That(rig.State.GrantedTransactions, Is.Empty, "not written down either");
        }

        [Test]
        public void a_new_install_records_its_first_launch_once()
        {
            var rig = new Rig(launched: false);
            Assert.That(rig.State.FirstLaunchUtcTicks, Is.EqualTo(FirstLaunch.Ticks));

            // A later launch keeps the first one
            var economy = new Economy(EconomyConfig.Parse(Json), rig.Store);
            new ShopGrants(economy, rig.Purchases, new EconomyShopCatalog(economy.Config), () => FirstLaunch.AddDays(5)).Initialize();
            Assert.That(rig.State.FirstLaunchUtcTicks, Is.EqualTo(FirstLaunch.Ticks));
        }

        // The device clock may run ahead of the store's: a purchase made now pays whatever its time says
        [Test]
        public void a_live_purchase_pays_even_with_the_clock_ahead()
        {
            var rig = new Rig();
            rig.Purchases.RaiseProductPurchased(new StoreTransaction("t4", "coins_2", FirstLaunch.AddMinutes(-5)));

            Assert.That(rig.Economy.Coins, Is.EqualTo(300));
        }

        [Test]
        public void restore_does_not_regrant_a_bundle()
        {
            var rig = new Rig();
            var starter = FakeRevenueCatGateway.Transaction("t5", "starter_pack");
            rig.Purchases.RaiseProductPurchased(starter);
            Assert.That((rig.Economy.Coins, rig.Economy.Count("bomb")), Is.EqualTo((400, 2)));

            // A restore brings back the same transaction with the premium entitlement
            rig.Purchases.RaiseTransactionsUpdated(new[] { starter, FakeRevenueCatGateway.Transaction("t6", "remove_ads") });
            Assert.That((rig.Economy.Coins, rig.Economy.Count("bomb")), Is.EqualTo((400, 2)));
        }

        [Test]
        public void remove_ads_grants_no_coins()
        {
            var rig = new Rig();
            rig.Purchases.RaiseProductPurchased(FakeRevenueCatGateway.Transaction("t7", "remove_ads"));

            Assert.That(rig.Economy.Coins, Is.EqualTo(0));
            Assert.That(rig.State.GrantedTransactions, Is.Empty);
        }

        [Test]
        public void a_product_not_in_the_catalog_grants_nothing()
        {
            var rig = new Rig();
            rig.Purchases.RaiseProductPurchased(FakeRevenueCatGateway.Transaction("t8", "coins_6"));
            rig.Purchases.RaiseTransactionsUpdated(new[] { FakeRevenueCatGateway.Transaction("t9", "premium_monthly") });

            Assert.That(rig.Economy.Coins, Is.EqualTo(0));
            Assert.That(rig.State.GrantedTransactions, Is.Empty);
        }

        [Test]
        public void without_an_economy_nothing_is_granted()
        {
            var rig = new Rig(json: null);
            rig.Purchases.RaiseProductPurchased(FakeRevenueCatGateway.Transaction("t1", "coins_2"));

            Assert.That(rig.Economy.Coins, Is.EqualTo(0));
            Assert.That(rig.State.GrantedTransactions, Is.Empty);
        }

        [Test]
        public void dispose_stops_listening()
        {
            var rig = new Rig();
            rig.Grants.Dispose();
            rig.Purchases.RaiseProductPurchased(FakeRevenueCatGateway.Transaction("t1", "coins_2"));

            Assert.That(rig.Economy.Coins, Is.EqualTo(0));
        }
    }
}
