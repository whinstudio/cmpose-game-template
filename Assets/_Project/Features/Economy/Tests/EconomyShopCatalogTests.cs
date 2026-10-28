using System.Linq;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    public sealed class EconomyShopCatalogTests
    {
        [Test]
        public void rows_follow_the_config_order()
        {
            var catalog = new EconomyShopCatalog(EconomyConfig.Parse(EconomyConfigTests.FullJson));

            Assert.That(catalog.Rows.Select(row => row.Id), Is.EqualTo(new[] { "starter_pack", "remove_ads", "no_ads_bundle", "coins_1" }));
            Assert.That(catalog.Row("no_ads_bundle").Coins, Is.EqualTo(600));
            Assert.That(catalog.Row("no_ads_bundle").Tag, Is.EqualTo(35));
            Assert.That(catalog.Row("coins_6"), Is.Null, "a row the shop does not sell");
            Assert.That(catalog.Row("remove_ads").PaysSomething, Is.False);
        }

        // An old plan's file without shop rows, or no economy at all: the shop sells Remove Ads only
        [Test]
        public void without_shop_rows_only_remove_ads()
        {
            var old = new EconomyShopCatalog(EconomyConfig.Parse(EconomyConfigTests.NoShop));
            var none = new EconomyShopCatalog(null);

            foreach (var catalog in new[] { old, none })
            {
                Assert.That(catalog.Rows.Select(row => row.Id), Is.EqualTo(new[] { EconomyShopCatalog.RemoveAds }));
                Assert.That(catalog.Rows[0].PaysSomething, Is.False);
            }
        }
    }
}
