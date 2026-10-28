using System.Collections.Generic;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    // The file's shop section (Compass writes it for #106 and the shop screen): optional, and whole when present
    public sealed class EconomyConfigTests
    {
        // EconomyTests.Json without its shop (the last section, which Without cannot take out)
        internal static readonly string NoShop = EconomyTests.Json.Substring(0, EconomyTests.Json.IndexOf(",\"shop\":", System.StringComparison.Ordinal)) + "}";

        private static readonly string Base = EconomyTests.Without(NoShop, "supply", "wheel", "login");

        internal static readonly string FullJson = Base.Replace("\"extras\":[]", "\"extras\":[]," +
            "\"shop\":{\"kind\":\"full\",\"rows\":[" +
            "{\"id\":\"starter_pack\",\"usd\":1.99,\"coins\":400,\"boosters\":{\"bomb\":2,\"shield\":2},\"tag\":90}," +
            "{\"id\":\"remove_ads\",\"usd\":4.99}," +
            "{\"id\":\"no_ads_bundle\",\"usd\":9.99,\"coins\":600,\"boosters\":{\"bomb\":3,\"shield\":3},\"tag\":35}," +
            "{\"id\":\"coins_1\",\"usd\":0.99,\"coins\":80}]}");

        [Test]
        public void reads_the_shop_section()
        {
            Assert.That(FullJson, Does.Contain("\"shop\""), "the base keeps an extras list to hang the shop on");
            var shop = EconomyConfig.Parse(FullJson).Shop;

            Assert.That(shop.Kind, Is.EqualTo(ShopConfig.Full));
            Assert.That(shop.Rows.Count, Is.EqualTo(4));
            var starter = shop.Rows[0];
            Assert.That((starter.Id, starter.Coins, starter.Tag), Is.EqualTo(("starter_pack", 400, (int?)90)));
            Assert.That(starter.Boosters, Is.EqualTo(new Dictionary<string, int> { ["bomb"] = 2, ["shield"] = 2 }));
            var removeAds = shop.Rows[1];
            Assert.That((removeAds.Id, removeAds.Coins, removeAds.Tag, removeAds.Boosters.Count), Is.EqualTo(("remove_ads", 0, (int?)null, 0)));
            Assert.That(shop.Rows[3].Coins, Is.EqualTo(80));
        }

        [Test]
        public void a_file_without_shop_is_simple_with_no_rows()
        {
            var config = EconomyConfig.Parse(NoShop);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.Shop.Kind, Is.EqualTo(ShopConfig.Simple));
            Assert.That(config.Shop.Rows, Is.Empty);
        }

        [Test]
        public void an_old_file_with_a_welcome_bundle_still_reads()
        {
            var old = FullJson.Replace("\"shop\":", "\"welcome\":{\"coins\":400,\"boosters\":{\"bomb\":3}},\"shop\":");

            Assert.That(old, Does.Contain("\"welcome\""));
            Assert.That(EconomyConfig.Parse(old), Is.Not.Null);
        }

        [Test]
        public void a_broken_shop_is_no_economy()
        {
            var broken = new[]
            {
                FullJson.Replace("\"kind\":\"full\"", "\"kind\":\"big\""),
                FullJson.Replace("\"kind\":\"full\",", ""),
                FullJson.Replace("\"rows\":[", "\"rows\":{\"x\":[").Replace("\"coins\":80}]", "\"coins\":80}]}"),
                FullJson.Replace("\"id\":\"coins_1\"", "\"id\":\"remove_ads\""),
                FullJson.Replace("\"id\":\"coins_1\"", "\"id\":\"Coins Pack\""),
                FullJson.Replace("\"coins\":80", "\"coins\":-80"),
                FullJson.Replace("\"coins\":80", "\"coins\":8.5"),
                FullJson.Replace("\"bomb\":2,", "\"magnet\":2,"),
                FullJson.Replace("\"bomb\":2,", "\"bomb\":0,"),
                FullJson.Replace("\"tag\":90", "\"tag\":0"),
                FullJson.Replace("\"tag\":90", "\"tag\":100"),
                FullJson.Replace("{\"id\":\"remove_ads\",\"usd\":4.99}", "\"remove_ads\"")
            };
            foreach (var json in broken) Assert.That(EconomyConfig.Parse(json), Is.Null, json);
        }
    }
}
