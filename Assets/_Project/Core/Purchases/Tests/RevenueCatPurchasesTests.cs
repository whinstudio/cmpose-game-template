using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class RevenueCatPurchasesTests
    {
        private const string Key = "test_key";

        private static RevenueCatPurchases Make(out FakeRevenueCatGateway gateway, out FakePreferences preferences, out FakeLog log,
            bool available = true)
        {
            gateway = new FakeRevenueCatGateway();
            preferences = new FakePreferences();
            log = new FakeLog();
            return new RevenueCatPurchases(gateway, preferences, log, Key, available);
        }

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        private static StoreTransaction Transaction(string id, string productId) => FakeRevenueCatGateway.Transaction(id, productId);

        private static StoreTransaction Transaction(string id, string productId, int minute) =>
            new StoreTransaction(id, productId, new System.DateTime(2026, 10, 28, 12, minute, 0, System.DateTimeKind.Utc));

        [Test]
        public void Unavailable_returns_null_products_and_error_purchases()
        {
            var purchases = Make(out var gateway, out _, out var log, available: false);
            purchases.Start();

            Assert.That(purchases.IsAvailable, Is.False);
            Assert.That(Run(purchases.ProductsAsync(CancellationToken.None)), Is.Null);
            Assert.That(Run(purchases.PurchaseAsync("coins_1", CancellationToken.None)).MessageKey, Is.EqualTo(PurchasesKeys.ErrorUnavailable));
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)).MessageKey, Is.EqualTo(PurchasesKeys.ErrorUnavailable));
            Assert.That(purchases.Transactions, Is.Empty);
            Assert.That(gateway.ConfiguredKeys, Is.Empty);
            Assert.That(gateway.CustomerCalls + gateway.OfferingCalls + gateway.Restores + gateway.Purchased.Count, Is.Zero);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PURCHASES available=false premium=false"));
        }

        [Test]
        public void Start_configures_with_the_key_and_follows_the_customer_info()
        {
            var purchases = Make(out var gateway, out var preferences, out var log);
            gateway.CustomerReply = StoreReply<CustomerSnapshot>.Ok(FakeRevenueCatGateway.Premium);
            var changes = 0;
            purchases.Changed += () => changes++;

            purchases.Start();

            Assert.That(gateway.ConfiguredKeys, Is.EqualTo(new[] { Key }));
            Assert.That(gateway.CustomerCalls, Is.EqualTo(1));
            Assert.That(purchases.IsPremium, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(preferences.Values[RevenueCatPurchases.PremiumKey], Is.EqualTo(true));
            Assert.That(preferences.Saves, Is.EqualTo(1));
            Assert.That(log.Lines.Where(l => l.StartsWith("MARKER")), Is.EqualTo(new[]
            {
                "MARKER CMPOSE_PURCHASES available=true premium=false",
                "MARKER CMPOSE_PURCHASES available=true premium=true"
            }));
        }

        [Test]
        public void Products_keep_the_offering_order_and_prices()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = StoreReply<IReadOnlyList<ShopProduct>>.Ok(new[]
            {
                new ShopProduct(ShopProducts.RemoveAds, "$4.99"), new ShopProduct("coins_1", "$0.99")
            });

            var products = Run(purchases.ProductsAsync(CancellationToken.None));

            Assert.That(products.Select(p => p.Id), Is.EqualTo(new[] { ShopProducts.RemoveAds, "coins_1" }));
            Assert.That(products.Select(p => p.PriceString), Is.EqualTo(new[] { "$4.99", "$0.99" }));
        }

        [Test]
        public void Products_are_null_without_a_current_offering_or_on_error()
        {
            var purchases = Make(out var gateway, out _, out var log);
            gateway.Offerings = StoreReply<IReadOnlyList<ShopProduct>>.Ok(null);
            Assert.That(Run(purchases.ProductsAsync(CancellationToken.None)), Is.Null);

            gateway.Offerings = StoreReply<IReadOnlyList<ShopProduct>>.Failed(StoreErrorKind.Network);
            Assert.That(Run(purchases.ProductsAsync(CancellationToken.None)), Is.Null);

            Assert.That(log.Lines.Count(l => l.StartsWith("WARNING")), Is.EqualTo(2));
        }

        [Test]
        public void Purchase_raises_ProductPurchased_with_the_transaction()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1", "coins_2");
            var t1 = Transaction("t1", "coins_2");
            gateway.PurchaseReply = FakeRevenueCatGateway.Bought(FakeRevenueCatGateway.Customer(new string[0], t1), t1);
            var bought = new List<StoreTransaction>();
            purchases.ProductPurchased += bought.Add;

            var result = Run(purchases.PurchaseAsync("coins_2", CancellationToken.None));

            Assert.That(result, Is.SameAs(PurchaseResult.Success));
            Assert.That(gateway.Purchased, Is.EqualTo(new[] { "coins_2" }));
            Assert.That(bought.Count, Is.EqualTo(1));
            Assert.That(bought[0].Id, Is.EqualTo("t1"));
            Assert.That(bought[0].ProductId, Is.EqualTo("coins_2"));
            Assert.That(purchases.IsPremium, Is.False, "coins unlock no entitlement");
        }

        [Test]
        public void A_purchase_is_reported_with_the_customer_info_id_of_its_transaction()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1");
            var earlier = Transaction("o1_old", "coins_1", 1);
            gateway.CustomerReply = StoreReply<CustomerSnapshot>.Ok(FakeRevenueCatGateway.Customer(new string[0], earlier));
            purchases.Start();
            // The store's id in the purchase result, RevenueCat's in the customer info (Test Store and Google Play)
            var store = Transaction("GPA.1234", "coins_1");
            var listed = Transaction("o1_new", "coins_1", 2);
            gateway.PurchaseReply = FakeRevenueCatGateway.Bought(FakeRevenueCatGateway.Customer(new string[0], earlier, listed), store);
            var bought = new List<StoreTransaction>();
            purchases.ProductPurchased += bought.Add;

            Run(purchases.PurchaseAsync("coins_1", CancellationToken.None));

            Assert.That(bought.Select(t => t.Id), Is.EqualTo(new[] { "o1_new" }));
        }

        [Test]
        public void A_purchase_whose_customer_info_came_first_is_still_reported_with_its_customer_info_id()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1");
            var earlier = Transaction("o1_old", "coins_1", 1);
            var listed = Transaction("o1_new", "coins_1", 2);
            gateway.CustomerReply = StoreReply<CustomerSnapshot>.Ok(FakeRevenueCatGateway.Customer(new string[0], earlier, listed));
            purchases.Start();
            gateway.PurchaseReply = FakeRevenueCatGateway.Bought(FakeRevenueCatGateway.Customer(new string[0], earlier, listed),
                Transaction("GPA.1234", "coins_1"));
            var bought = new List<StoreTransaction>();
            purchases.ProductPurchased += bought.Add;

            Run(purchases.PurchaseAsync("coins_1", CancellationToken.None));

            Assert.That(bought.Select(t => t.Id), Is.EqualTo(new[] { "o1_new" }));
        }

        [Test]
        public void Remove_ads_purchase_turns_premium_on()
        {
            var purchases = Make(out var gateway, out var preferences, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering(ShopProducts.RemoveAds);
            var t = Transaction("t1", ShopProducts.RemoveAds);
            gateway.PurchaseReply = FakeRevenueCatGateway.Bought(FakeRevenueCatGateway.Customer(new[] { PurchasesConfig.PremiumEntitlement }, t), t);
            var changes = 0;
            purchases.Changed += () => changes++;

            var result = Run(purchases.PurchaseAsync(ShopProducts.RemoveAds, CancellationToken.None));

            Assert.That(result, Is.SameAs(PurchaseResult.Success));
            Assert.That(purchases.IsPremium, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(preferences.Values[RevenueCatPurchases.PremiumKey], Is.EqualTo(true));
        }

        [Test]
        public void A_purchase_without_a_transaction_succeeds_and_raises_no_product_event()
        {
            var purchases = Make(out var gateway, out _, out var log);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1");
            gateway.PurchaseReply = FakeRevenueCatGateway.Bought(FakeRevenueCatGateway.Free, null);
            var bought = 0;
            purchases.ProductPurchased += _ => bought++;

            Assert.That(Run(purchases.PurchaseAsync("coins_1", CancellationToken.None)), Is.SameAs(PurchaseResult.Success));
            Assert.That(bought, Is.Zero);
            Assert.That(log.Lines, Has.Some.StartsWith("WARNING"));
        }

        [Test]
        public void Owns_a_one_time_product_from_transactions()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.CustomerReply = StoreReply<CustomerSnapshot>.Ok(
                FakeRevenueCatGateway.Customer(new string[0], Transaction("t1", ShopProducts.StarterPack)));

            Assert.That(purchases.Owns(ShopProducts.StarterPack), Is.False, "nothing is known before RevenueCat answers");
            purchases.Start();

            Assert.That(purchases.Owns(ShopProducts.StarterPack), Is.True);
            Assert.That(purchases.Owns("coins_1"), Is.False);
        }

        [Test]
        public void Refresh_raises_TransactionsUpdated()
        {
            var purchases = Make(out var gateway, out _, out _);
            var both = new[] { Transaction("t1", "coins_1"), Transaction("t2", "bundle_1") };
            gateway.CustomerReply = StoreReply<CustomerSnapshot>.Ok(FakeRevenueCatGateway.Customer(new string[0], both));
            var updates = new List<IReadOnlyList<StoreTransaction>>();
            purchases.TransactionsUpdated += updates.Add;

            purchases.Start();

            Assert.That(updates.Count, Is.EqualTo(1));
            Assert.That(updates[0].Select(t => t.Id), Is.EqualTo(new[] { "t1", "t2" }));
            Assert.That(purchases.Transactions.Select(t => t.Id), Is.EqualTo(new[] { "t1", "t2" }));
        }

        [Test]
        public void A_customer_info_push_raises_TransactionsUpdated_without_a_purchase_event()
        {
            var purchases = Make(out var gateway, out _, out _);
            purchases.Start();
            var updates = new List<IReadOnlyList<StoreTransaction>>();
            var bought = 0;
            purchases.TransactionsUpdated += updates.Add;
            purchases.ProductPurchased += _ => bought++;

            gateway.Push(FakeRevenueCatGateway.Customer(new string[0], Transaction("t9", "coins_3")));

            Assert.That(updates.Single().Single().Id, Is.EqualTo("t9"));
            Assert.That(bought, Is.Zero, "a purchase the app missed reaches the economy through TransactionsUpdated only");
        }

        [Test]
        public void A_cancelled_purchase_is_cancelled_and_store_errors_become_keys()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1");
            var bought = 0;
            purchases.ProductPurchased += _ => bought++;

            gateway.PurchaseReply = StoreReply<PurchaseReply>.Failed(StoreErrorKind.Cancelled);
            Assert.That(Run(purchases.PurchaseAsync("coins_1", CancellationToken.None)), Is.SameAs(PurchaseResult.Cancelled));

            gateway.PurchaseReply = StoreReply<PurchaseReply>.Failed(StoreErrorKind.Network);
            var error = Run(purchases.PurchaseAsync("coins_1", CancellationToken.None));
            Assert.That(error.Outcome, Is.EqualTo(PurchaseOutcome.Error));
            Assert.That(error.MessageKey, Is.EqualTo("purchases.error.network"));
            Assert.That(purchases.IsPremium, Is.False);
            Assert.That(bought, Is.Zero);
        }

        [Test]
        public void A_product_not_in_the_offering_is_package_unavailable()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = FakeRevenueCatGateway.Offering("coins_1");

            var missing = Run(purchases.PurchaseAsync(ShopProducts.StarterPack, CancellationToken.None));
            Assert.That(missing.MessageKey, Is.EqualTo(PurchasesKeys.ErrorPackageUnavailable));
            Assert.That(gateway.OfferingCalls, Is.EqualTo(1));
            Assert.That(gateway.Purchased, Is.Empty);

            // Opened before RevenueCat answered: the first purchase fetches the products, then buys
            var fresh = Make(out var other, out _, out _);
            other.Offerings = FakeRevenueCatGateway.Offering("coins_1");
            Assert.That(Run(fresh.PurchaseAsync("coins_1", CancellationToken.None)), Is.SameAs(PurchaseResult.Success));
            Assert.That(other.OfferingCalls, Is.EqualTo(1));
            Assert.That(other.Purchased, Is.EqualTo(new[] { "coins_1" }));
        }

        [Test]
        public void Restore_without_premium_is_nothing_to_restore()
        {
            var purchases = Make(out var gateway, out _, out _);
            var bought = 0;
            purchases.ProductPurchased += _ => bought++;

            gateway.RestoreReply = StoreReply<CustomerSnapshot>.Ok(
                FakeRevenueCatGateway.Customer(new string[0], Transaction("t1", ShopProducts.StarterPack)));
            var nothing = Run(purchases.RestoreAsync(CancellationToken.None));
            Assert.That(nothing.MessageKey, Is.EqualTo(PurchasesKeys.ErrorNothingToRestore));

            gateway.RestoreReply = StoreReply<CustomerSnapshot>.Ok(FakeRevenueCatGateway.Premium);
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)), Is.SameAs(PurchaseResult.Success));
            Assert.That(purchases.IsPremium, Is.True);

            gateway.RestoreReply = StoreReply<CustomerSnapshot>.Failed(StoreErrorKind.Store);
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)).MessageKey, Is.EqualTo("purchases.error.store"));
            Assert.That(gateway.Restores, Is.EqualTo(3));
            Assert.That(bought, Is.Zero, "a restore is not a purchase");
        }

        [Test]
        public void A_cached_premium_shows_before_revenuecat_answers_and_follows_it()
        {
            var gateway = new FakeRevenueCatGateway
            {
                PendingCustomer = new UniTaskCompletionSource<StoreReply<CustomerSnapshot>>()
            };
            var preferences = new FakePreferences();
            preferences.SetBool(RevenueCatPurchases.PremiumKey, true);
            var log = new FakeLog();
            var purchases = new RevenueCatPurchases(gateway, preferences, log, Key, true);
            var changes = 0;
            purchases.Changed += () => changes++;

            purchases.Start();
            Assert.That(purchases.IsPremium, Is.True);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PURCHASES available=true premium=true"));

            gateway.Push(FakeRevenueCatGateway.Free);
            Assert.That(purchases.IsPremium, Is.False);
            Assert.That(preferences.Values[RevenueCatPurchases.PremiumKey], Is.EqualTo(false));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void The_same_state_again_fires_nothing()
        {
            var purchases = Make(out var gateway, out var preferences, out _);
            purchases.Start();
            var changes = 0;
            purchases.Changed += () => changes++;

            gateway.Push(FakeRevenueCatGateway.Free);
            gateway.Push(FakeRevenueCatGateway.Customer(new[] { "something-else" }));

            Assert.That(changes, Is.Zero);
            Assert.That(preferences.Saves, Is.Zero);
        }

        [Test]
        public void It_is_the_premium_status_and_announces_premium_changes()
        {
            var purchases = Make(out var gateway, out _, out _);
            IPremiumStatus status = purchases;
            var changes = 0;
            status.Changed += () => changes++;

            purchases.Start();
            gateway.Push(FakeRevenueCatGateway.Premium);

            Assert.That(status.IsPremium, Is.True);
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
