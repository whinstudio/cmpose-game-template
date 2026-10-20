using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class PurchasesPremiumEventsTests
    {
        private const string Json =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}]," +
            "\"welcome\":{\"coins\":400,\"boosters\":{\"bomb\":3}}}";

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        [Test]
        public void Welcome_bundle_follows_a_purchase_not_a_restore_or_a_premium_launch()
        {
            var gateway = new FakeRevenueCatGateway
            {
                Offerings = StoreReply<System.Collections.Generic.IReadOnlyList<StorePackage>>.Ok(new[]
                    { new StorePackage("$rc_annual", "ANNUAL", "$59.99", 59.99, "USD", null, null) }),
                CustomerReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium),
                RestoreReply = StoreReply<System.Collections.Generic.IReadOnlyCollection<string>>.Ok(FakeRevenueCatGateway.Premium)
            };
            var purchases = new RevenueCatPurchases(gateway, new FakePreferences(), new FakeLog(), "test_key", true);
            var economy = new Economy(EconomyConfig.Parse(Json), new MemoryEconomyStore());
            var bundle = new WelcomeBundle(economy, new PurchasesPremiumEvents(purchases));
            bundle.Initialize();

            purchases.Start();
            Run(purchases.RestoreAsync(CancellationToken.None));
            Assert.That(economy.Coins, Is.Zero, "premium found at launch or restored gives no bundle");

            Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));
            Run(purchases.PurchaseAsync("$rc_annual", CancellationToken.None));
            Assert.That(economy.Coins, Is.EqualTo(400), "the bundle is paid once");
            Assert.That(economy.Count("bomb"), Is.EqualTo(3));
            bundle.Dispose();
        }
    }
}
