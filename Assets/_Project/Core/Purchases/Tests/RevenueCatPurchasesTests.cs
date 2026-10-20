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

        private static StoreReply<System.Collections.Generic.IReadOnlyList<StorePackage>> Offering(params StorePackage[] packages) =>
            StoreReply<System.Collections.Generic.IReadOnlyList<StorePackage>>.Ok(packages);

        private static StorePackage Monthly => new StorePackage("$rc_monthly", "MONTHLY", "$9.99", 9.99, "USD", null, null);
        private static StorePackage Annual => new StorePackage("$rc_annual", "ANNUAL", "$59.99", 59.99, "USD", "$4.99", null);

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        [Test]
        public void Unavailable_purchases_never_touch_the_sdk()
        {
            var purchases = Make(out var gateway, out _, out var log, available: false);
            purchases.Start();

            Assert.That(purchases.IsAvailable, Is.False);
            Assert.That(Run(purchases.OfferAsync(CancellationToken.None)), Is.Null);
            Assert.That(Run(purchases.PurchaseAsync("$rc_monthly", CancellationToken.None)).MessageKey, Is.EqualTo(PurchasesKeys.ErrorUnavailable));
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)).MessageKey, Is.EqualTo(PurchasesKeys.ErrorUnavailable));
            Assert.That(gateway.ConfiguredKeys, Is.Empty);
            Assert.That(gateway.CustomerCalls + gateway.OfferingCalls + gateway.Restores + gateway.Purchased.Count, Is.Zero);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PURCHASES available=false premium=false"));
        }

        [Test]
        public void Start_configures_with_the_key_and_follows_the_customer_info()
        {
            var purchases = Make(out var gateway, out var preferences, out var log);
            gateway.CustomerReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium);
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
        public void Offer_maps_the_packages_in_order()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = Offering(Monthly, Annual);

            var offer = Run(purchases.OfferAsync(CancellationToken.None));

            Assert.That(offer.Packages.Select(p => p.Id), Is.EqualTo(new[] { "$rc_monthly", "$rc_annual" }));
            Assert.That(offer.Packages[1].SavingsPercent, Is.EqualTo(50));
            Assert.That(offer.Packages[1].PricePerMonth, Is.EqualTo("$4.99"));
        }

        [Test]
        public void Offer_is_null_without_a_current_offering_or_on_error()
        {
            var purchases = Make(out var gateway, out _, out var log);
            gateway.Offerings = StoreReply<System.Collections.Generic.IReadOnlyList<StorePackage>>.Ok(null);
            Assert.That(Run(purchases.OfferAsync(CancellationToken.None)), Is.Null);

            gateway.Offerings = StoreReply<System.Collections.Generic.IReadOnlyList<StorePackage>>.Failed(StoreErrorKind.Network);
            Assert.That(Run(purchases.OfferAsync(CancellationToken.None)), Is.Null);

            Assert.That(log.Lines.Count(l => l.StartsWith("WARNING")), Is.EqualTo(2));
        }

        [Test]
        public void A_purchase_turns_premium_on_and_fires_changed_once()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = Offering(Monthly, Annual);
            Run(purchases.OfferAsync(CancellationToken.None));
            var changes = 0;
            purchases.Changed += () => changes++;

            var result = Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));

            Assert.That(result, Is.SameAs(PurchaseResult.Success));
            Assert.That(gateway.Purchased, Is.EqualTo(new[] { "$rc_annual" }));
            Assert.That(purchases.IsPremium, Is.True);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void A_cancelled_purchase_is_cancelled_and_store_errors_become_keys()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = Offering(Monthly);
            Run(purchases.OfferAsync(CancellationToken.None));

            gateway.PurchaseReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Failed(StoreErrorKind.Cancelled);
            Assert.That(Run(purchases.PurchaseAsync("$rc_monthly", CancellationToken.None)), Is.SameAs(PurchaseResult.Cancelled));

            gateway.PurchaseReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Failed(StoreErrorKind.Network);
            var error = Run(purchases.PurchaseAsync("$rc_monthly", CancellationToken.None));
            Assert.That(error.Outcome, Is.EqualTo(PurchaseOutcome.Error));
            Assert.That(error.MessageKey, Is.EqualTo("purchases.error.network"));
            Assert.That(purchases.IsPremium, Is.False);
        }

        [Test]
        public void An_unknown_package_asks_for_the_offer_once_then_fails_without_buying()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = Offering(Monthly);

            var missing = Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));
            Assert.That(missing.MessageKey, Is.EqualTo(PurchasesKeys.ErrorPackageUnavailable));
            Assert.That(gateway.OfferingCalls, Is.EqualTo(1));
            Assert.That(gateway.Purchased, Is.Empty);

            // Opened before RevenueCat answered: the first purchase fetches the offer, then buys
            var fresh = Make(out var other, out _, out _);
            other.Offerings = Offering(Monthly);
            Assert.That(Run(fresh.PurchaseAsync("$rc_monthly", CancellationToken.None)), Is.SameAs(PurchaseResult.Success));
            Assert.That(other.OfferingCalls, Is.EqualTo(1));
            Assert.That(other.Purchased, Is.EqualTo(new[] { "$rc_monthly" }));
        }

        [Test]
        public void Restore_succeeds_only_when_it_brings_premium_back()
        {
            var purchases = Make(out var gateway, out _, out _);

            var nothing = Run(purchases.RestoreAsync(CancellationToken.None));
            Assert.That(nothing.MessageKey, Is.EqualTo(PurchasesKeys.ErrorNothingToRestore));

            gateway.RestoreReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium);
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)), Is.SameAs(PurchaseResult.Success));
            Assert.That(purchases.IsPremium, Is.True);

            gateway.RestoreReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Failed(StoreErrorKind.Store);
            Assert.That(Run(purchases.RestoreAsync(CancellationToken.None)).MessageKey, Is.EqualTo("purchases.error.store"));
            Assert.That(gateway.Restores, Is.EqualTo(3));
        }

        [Test]
        public void A_cached_premium_shows_before_revenuecat_answers_and_follows_it()
        {
            var gateway = new FakeRevenueCatGateway
            {
                PendingCustomer = new UniTaskCompletionSource<StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>>()
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
            gateway.Push(new[] { "something-else" });

            Assert.That(changes, Is.Zero);
            Assert.That(preferences.Saves, Is.Zero);
        }

        [Test]
        public void Premium_purchased_fires_on_a_purchase_only()
        {
            var purchases = Make(out var gateway, out _, out _);
            gateway.Offerings = Offering(Monthly, Annual);
            var purchased = 0;
            purchases.PremiumPurchased += () => purchased++;

            gateway.CustomerReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium);
            purchases.Start();
            gateway.RestoreReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium);
            Run(purchases.RestoreAsync(CancellationToken.None));
            gateway.PurchaseReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Failed(StoreErrorKind.Cancelled);
            Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));
            Assert.That(purchased, Is.Zero, "a refresh, a restore and a cancelled purchase are not purchases");

            gateway.PurchaseReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium);
            Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));
            Assert.That(purchased, Is.EqualTo(1));
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
