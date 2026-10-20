using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Features.Shell.GameShell;
using Kturker.UnityBase.Tests.EditMode;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    public sealed class EconomyTests
    {
        // The v1 file Compass writes (two boosters so the wheel and the welcome bundle pay both kinds; the three ready
        // extras)
        internal const string Json =
            "{\"version\":1,\"currency\":{\"name\":\"acorns\",\"color\":\"#B5651D\",\"icon\":\"Art/game-icons/currency\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"icon\":\"Art/game-icons/bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":2}," +
            "{\"id\":\"shield\",\"icon\":\"Art/game-icons/shield\",\"price\":120,\"offer\":\"preRun\",\"start\":1}]," +
            "\"supply\":{\"freeCoins\":15,\"freeHours\":3,\"adCoins\":[20,40,50,75]}," +
            "\"wheel\":{\"freeEvery\":4,\"adSpinsPerDay\":3," +
            "\"slices\":[{\"coins\":15,\"weight\":30},{\"coins\":30,\"weight\":25},{\"coins\":60,\"weight\":15}," +
            "{\"coins\":150,\"weight\":5},{\"booster\":\"bomb\",\"count\":1,\"weight\":15},{\"booster\":\"shield\",\"count\":1,\"weight\":10}]}," +
            "\"login\":[{\"coins\":15},{\"coins\":15},{\"coins\":30},{\"coins\":30},{\"coins\":45},{\"coins\":45},{\"coins\":200,\"booster\":\"bomb\",\"count\":1}]," +
            "\"extras\":[{\"id\":\"daily\",\"kind\":\"supply\"},{\"id\":\"spin\",\"kind\":\"wheel\"},{\"id\":\"streak\",\"kind\":\"login\"}]," +
            "\"welcome\":{\"coins\":400,\"boosters\":{\"bomb\":3}}}";

        // A custom extra first, then the supply and the wheel (no login bonus)
        internal static readonly string CustomJson =
            Without(Json, "login").Replace("\"extras\":[", "\"extras\":[{\"id\":\"scratch\",\"kind\":\"custom\"},");

        private const long Hour = 3600L * 1000L;
        // 2026-10-20 12:00 at UTC+3 (09:00 UTC)
        private const long Noon = 1792486800000L;

        private static readonly TimeZoneInfo Zone =
            TimeZoneInfo.CreateCustomTimeZone("Test+03", TimeSpan.FromHours(3), "Test+03", "Test+03");

        private sealed class FakePremiumPurchaseEvents : IPremiumPurchaseEvents
        {
            public event Action PremiumPurchased;
            public void Purchase() => PremiumPurchased?.Invoke();
        }

        private sealed class Rig
        {
            public readonly MemoryEconomyStore Store = new MemoryEconomyStore();
            public readonly FakeClock Clock = new FakeClock { NowMillis = Noon };
            public readonly FakeEconomyAds Ads = new FakeEconomyAds();
            public bool AutoPlay;
            public readonly Economy Economy;
            public readonly Supply Supply;
            public readonly Wheel Wheel;
            public readonly LoginBonus Login;

            public Rig(string json = Json)
            {
                Economy = new Economy(json == null ? null : EconomyConfig.Parse(json), Store);
                Supply = new Supply(Economy, Ads, Clock, () => AutoPlay, Zone);
                Wheel = new Wheel(Economy, Ads, Clock, () => AutoPlay, Zone);
                Login = new LoginBonus(Economy, Clock, () => AutoPlay, Zone);
            }
        }

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        [TearDown]
        public void ResetConfig() => EconomyConfig.ForTests(null);

        [Test]
        public void Config_reads_the_v1_file()
        {
            var config = EconomyConfig.Parse(Json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.Boosters.Count, Is.EqualTo(2));
            Assert.That(config.Booster("bomb").Offer, Is.EqualTo(BoosterOffer.InRun));
            Assert.That(config.Booster("shield").NameKey, Is.EqualTo("economy.booster.shield.name"));
            Assert.That(config.Supply.FreeInterval, Is.EqualTo(TimeSpan.FromHours(3)));
            Assert.That(config.Supply.AdCoins, Is.EqualTo(new[] { 20, 40, 50, 75 }));
            Assert.That(config.Wheel.Slices.Count, Is.EqualTo(6));
            Assert.That(config.Login[6].Booster, Is.EqualTo("bomb"));
            Assert.That(config.Welcome.Boosters["bomb"], Is.EqualTo(3));
        }

        [Test]
        public void Broken_or_tampered_config_is_no_economy()
        {
            var broken = new[]
            {
                "not json",
                Json.Replace("\"version\":1", "\"version\":2"),
                Json.Replace("\"price\":80", "\"price\":-80"),
                Json.Replace("\"price\":80", "\"price\":0"),
                Json.Replace("\"id\":\"bomb\"", "\"id\":\"Bomb Key\""),
                Json.Replace("\"id\":\"shield\"", "\"id\":\"bomb\""),
                Json.Replace("\"offer\":\"inRun\"", "\"offer\":\"anytime\""),
                Json.Replace("{\"booster\":\"bomb\",\"count\":1,\"weight\":15}", "{\"booster\":\"magnet\",\"count\":1,\"weight\":15}"),
                Json.Replace("\"weight\":30", "\"weight\":0"),
                Json.Replace("{\"coins\":15},{\"coins\":15},", "{\"coins\":15},"),
                Json.Replace("\"freeHours\":3", "\"freeHours\":0"),
                Json.Replace("\"freeCoins\":15", "\"freeCoins\":1.5"),
                Json.Replace("\"boosters\":{\"bomb\":3}", "\"boosters\":{\"magnet\":3}"),
                // The currency: a name of 1-24 characters, a "#RRGGBB" colour, an optional icon path; never absent
                Json.Replace("\"name\":\"acorns\",", ""),
                Json.Replace("\"name\":\"acorns\"", "\"name\":\"   \""),
                Json.Replace("\"name\":\"acorns\"", "\"name\":\"acorns acorns acorns acorns\""),
                Json.Replace("\"color\":\"#B5651D\",", ""),
                Json.Replace("\"#B5651D\"", "\"brown\""),
                Json.Replace("\"#B5651D\"", "\"#B56\""),
                Json.Replace("\"#B5651D\"", "\"#GG651D\""),
                Json.Replace("\"icon\":\"Art/game-icons/currency\"", "\"icon\":5"),
                Json.Replace("\"currency\":{\"name\":\"acorns\",\"color\":\"#B5651D\",\"icon\":\"Art/game-icons/currency\"}", "\"currency\":\"acorns\""),
                Json.Replace("\"currency\":{\"name\":\"acorns\",\"color\":\"#B5651D\",\"icon\":\"Art/game-icons/currency\"},", ""),
                // The extras: a known kind, unique ids, a ready kind once, at most MaxExtras
                CustomJson.Replace("\"kind\":\"custom\"", "\"kind\":\"chest\""),
                CustomJson.Replace("{\"id\":\"scratch\",\"kind\":\"custom\"}", "{\"id\":\"scratch\"}"),
                CustomJson.Replace("\"id\":\"scratch\"", "\"id\":\"daily\""),
                CustomJson.Replace("\"id\":\"scratch\"", "\"id\":\"Scratch Card\""),
                CustomJson.Replace("{\"id\":\"scratch\",\"kind\":\"custom\"}", "{\"id\":\"scratch\",\"kind\":\"supply\"}"),
                Json.Replace("\"extras\":[", "\"extras\":[{\"id\":\"chest\",\"kind\":\"custom\"},"),
                Json.Replace("\"extras\":[", "\"extras\":{\"x\":[").Replace("\"kind\":\"login\"}]", "\"kind\":\"login\"}]}"),
                // A section that is there must be whole
                Json.Replace("\"supply\":{\"freeCoins\":15,\"freeHours\":3,\"adCoins\":[20,40,50,75]}", "\"supply\":{}"),
                Json.Replace("\"wheel\":{\"freeEvery\":4,", "\"wheel\":{"),
                Json.Replace("\"login\":[{\"coins\":15},", "\"login\":null,\"x\":[{\"coins\":15},")
            };
            foreach (var json in broken) Assert.That(EconomyConfig.Parse(json), Is.Null, json);
        }

        // The planner names the game's own currency: any name, its colour, an icon or none
        [Test]
        public void Config_reads_a_free_currency()
        {
            var acorns = EconomyConfig.Parse(Json).Currency;
            Assert.That((acorns.Name, acorns.Icon), Is.EqualTo(("acorns", "Art/game-icons/currency")));
            Assert.That(acorns.Colour.r, Is.EqualTo(0xB5 / 255f).Within(0.002f));
            Assert.That(acorns.Colour.g, Is.EqualTo(0x65 / 255f).Within(0.002f));
            Assert.That(acorns.Colour.b, Is.EqualTo(0x1D / 255f).Within(0.002f));

            var sparks = EconomyConfig.Parse(Json.Replace(
                "{\"name\":\"acorns\",\"color\":\"#B5651D\",\"icon\":\"Art/game-icons/currency\"}",
                "{\"name\":\" Moon sparks \",\"color\":\"#8e44ad\"}")).Currency;
            Assert.That((sparks.Name, sparks.Icon), Is.EqualTo(("Moon sparks", (string)null)), "trimmed; the icon is optional");
            Assert.That(sparks.Colour.b, Is.EqualTo(0xAD / 255f).Within(0.002f), "lower-case hex");

            var longest = EconomyConfig.Parse(Json.Replace("\"acorns\"", "\"abcdefghijklmnopqrstuvwx\""));
            Assert.That(longest.Currency.Name.Length, Is.EqualTo(EconomyConfig.MaxCurrencyName));
        }

        [Test]
        public void Extras_list_defaults_to_empty()
        {
            var config = EconomyConfig.Parse(CustomJson);
            Assert.That(config.Extras.Select(e => (e.Id, e.Kind)), Is.EqualTo(new[]
            {
                ("scratch", ExtraKind.Custom), ("daily", ExtraKind.Supply), ("spin", ExtraKind.Wheel)
            }));
            Assert.That(config.Extra("scratch").NameKey, Is.EqualTo("economy.extra.scratch.name"));
            Assert.That(config.Extra("nope"), Is.Null);

            // No extras: the field is absent and the list is empty
            var none = EconomyConfig.Parse(Without(Json, "supply", "wheel", "login", "extras"));
            Assert.That(none, Is.Not.Null);
            Assert.That(none.Extras, Is.Empty);
            var empty = EconomyConfig.Parse(Without(Json, "supply", "wheel", "login"));
            Assert.That(empty, Is.Not.Null);
            Assert.That(empty.Extras, Is.Empty, "an empty list");

            // Only custom extras: no ready section is needed
            var custom = EconomyConfig.Parse(Without(CustomJson, "supply", "wheel"));
            Assert.That(custom.Extras.Select(e => e.Id), Is.EqualTo(new[] { "scratch" }));
            Assert.That((custom.Supply, custom.Wheel, custom.Login), Is.EqualTo(((SupplyConfig)null, (WheelConfig)null, (IReadOnlyList<EconomyReward>)null)));
        }

        // A ready extra's section and its entry in extras come together; one without the other is a broken file
        [Test]
        public void Ready_section_without_its_extra_turns_the_economy_off()
        {
            foreach (var kind in new[] { "supply", "wheel", "login" })
            {
                var sectionOnly = WithoutEntry(Json, kind);
                Assert.That((sectionOnly.Contains($"\"{kind}\":"), sectionOnly.Contains($"\"kind\":\"{kind}\"")), Is.EqualTo((true, false)));
                Assert.That(EconomyConfig.Parse(sectionOnly), Is.Null, $"{kind}: the section without its extra");
                var entryOnly = WithoutSection(Json, kind);
                Assert.That(EconomyConfig.Parse(entryOnly), Is.Null, $"{kind}: the extra without its section");
                Assert.That(EconomyConfig.Parse(Without(Json, kind)), Is.Not.Null, $"{kind}: both left out");
            }
            Assert.That(EconomyConfig.Parse(Without(Json, "extras")), Is.Null, "ready sections without any extras");
        }

        // Compass leaves out the extras that do not suit the game: the economy stays on with the currency, the boosters
        // and the welcome bundle, and each missing extra is off (its section and its entry in extras both go)
        internal static string Without(string json, params string[] sections)
        {
            foreach (var section in sections)
            {
                json = WithoutEntry(WithoutSection(json, section), section);
            }
            return json;
        }

        // The extras entry of a kind taken out of the list
        private static string WithoutEntry(string json, string kind) =>
            Regex.Replace(json, $"\\{{\"id\":\"[a-z0-9_]+\",\"kind\":\"{kind}\"\\}},?", "").Replace(",]", "]");

        private static string WithoutSection(string json, params string[] sections)
        {
            foreach (var section in sections)
            {
                var at = json.IndexOf($"\"{section}\":", StringComparison.Ordinal);
                var open = json[at + section.Length + 3];
                var close = open == '[' ? ']' : '}';
                var depth = 0;
                var end = at + section.Length + 3;
                for (; end < json.Length; end++)
                {
                    if (json[end] == open) depth++;
                    else if (json[end] == close && --depth == 0) break;
                }
                json = json.Remove(at, end - at + 2);
            }
            return json;
        }

        [Test]
        public void Missing_sections_turn_their_features_off()
        {
            var json = Without(Json, "supply", "wheel", "login");
            var config = EconomyConfig.Parse(json);
            Assert.That(config, Is.Not.Null, json);
            Assert.That((config.Supply, config.Wheel, config.Login), Is.EqualTo(((SupplyConfig)null, (WheelConfig)null, (IReadOnlyList<EconomyReward>)null)));
            Assert.That(config.Welcome.Coins, Is.EqualTo(400));

            var rig = new Rig(json);
            Assert.That(rig.Economy.Enabled, Is.True);
            rig.Economy.Earn(10);
            Assert.That(rig.Economy.Coins, Is.EqualTo(10), "the currency still works");
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(2), "and the boosters with their start stock");

            Assert.That(rig.Supply.Configured, Is.False);
            Assert.That(rig.Supply.FreeReady, Is.False);
            Assert.That(rig.Supply.FreeReadyIn, Is.EqualTo(TimeSpan.Zero));
            Assert.That(rig.Supply.NextAdRow, Is.EqualTo(-1));
            Assert.That(rig.Supply.AdRowReady, Is.False);
            Assert.That(rig.Supply.ClaimFree(), Is.False);
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.False);

            var runs = new ShellRunEvents();
            var counter = new WheelRunCounter(runs, rig.Wheel);
            counter.Initialize();
            for (var i = 0; i < 5; i++) runs.Report(ShellMode.Endless, true);
            counter.Dispose();
            Assert.That(rig.Wheel.Configured, Is.False);
            Assert.That(rig.Economy.Wallet.State.WheelProgress, Is.Zero, "no wheel: runs are not counted");
            Assert.That((rig.Wheel.Progress, rig.Wheel.FreeEvery, rig.Wheel.Slices.Count, rig.Wheel.AdSpinsLeft), Is.EqualTo((0, 0, 0, 0)));
            Assert.That(rig.Wheel.Spin(new Random(1)), Is.Null);
            Assert.That(Run(rig.Wheel.SpinWithAdAsync(new Random(1), CancellationToken.None)), Is.Null);

            Assert.That(rig.Login.Configured, Is.False);
            Assert.That(rig.Login.ReadyToday, Is.False);
            Assert.That(rig.Login.Rewards, Is.Empty);
            Assert.That(rig.Login.Claim(), Is.Null);

            Assert.That(rig.Ads.Shows, Is.Zero, "no ad was asked for");
            Assert.That(rig.Economy.Coins, Is.EqualTo(10), "nothing was paid");

            // One feature alone: the others stay off
            var supplyOnly = new Rig(Without(Json, "wheel", "login"));
            Assert.That((supplyOnly.Supply.FreeReady, supplyOnly.Wheel.Configured, supplyOnly.Login.ReadyToday), Is.EqualTo((true, false, false)));
        }

        [Test]
        public void Absent_config_disables_the_economy()
        {
            var rig = new Rig(null);
            var changes = 0;
            rig.Economy.Changed += () => changes++;

            rig.Economy.Earn(100);
            rig.Economy.Add("bomb", 3);

            Assert.That(rig.Economy.Enabled, Is.False);
            Assert.That(rig.Economy.Config, Is.Null);
            Assert.That(rig.Economy.Coins, Is.Zero);
            Assert.That(rig.Economy.TrySpend(0), Is.False);
            Assert.That(rig.Economy.TryUse("bomb"), Is.False);
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);
            Assert.That(changes, Is.Zero);
            Assert.That(rig.Supply.FreeReady, Is.False);
            Assert.That(rig.Supply.NextAdRow, Is.EqualTo(-1));
            Assert.That(rig.Wheel.FreeSpinReady, Is.False);
            Assert.That(rig.Wheel.AdSpinsLeft, Is.Zero);
            Assert.That(rig.Login.ReadyToday, Is.False);
            Assert.That(rig.Login.Claim(), Is.Null);

            // The template ships without Resources/GameEconomy.json; a game Compass built has one
            EconomyConfig.ForTests(null);
            if (UnityEngine.Resources.Load<UnityEngine.TextAsset>(EconomyConfig.ResourcePath) == null)
                Assert.That(EconomyConfig.Current, Is.Null);
        }

        [Test]
        public void Start_inventory_is_granted_once()
        {
            var rig = new Rig();
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(2));
            Assert.That(rig.Economy.Count("shield"), Is.EqualTo(1));
            rig.Economy.TryUse("bomb");

            var again = new Economy(EconomyConfig.Parse(Json), rig.Store);

            Assert.That(again.Count("bomb"), Is.EqualTo(1), "a second launch adds no start inventory");
            Assert.That(again.Count("shield"), Is.EqualTo(1));
        }

        [Test]
        public void Earn_and_spend_update_coins_and_raise_changed()
        {
            var rig = new Rig();
            var changes = 0;
            rig.Economy.Changed += () => changes++;

            rig.Economy.Earn(100);
            rig.Economy.Earn(-50);
            rig.Economy.Earn(0);
            Assert.That(rig.Economy.Coins, Is.EqualTo(100));
            Assert.That(changes, Is.EqualTo(1), "nothing earned raises nothing");

            Assert.That(rig.Economy.TrySpend(80), Is.True);
            Assert.That(rig.Economy.TrySpend(80), Is.False, "not enough coins");
            Assert.That(rig.Economy.TrySpend(-10), Is.False, "a negative price is refused");
            Assert.That(rig.Economy.Coins, Is.EqualTo(20));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(rig.Store.Load().Coins, Is.EqualTo(20), "every change is saved");
        }

        [Test]
        public void TryUse_consumes_one_and_fails_at_zero()
        {
            var rig = new Rig();

            Assert.That(rig.Economy.TryUse("bomb"), Is.True);
            Assert.That(rig.Economy.TryUse("bomb"), Is.True);
            Assert.That(rig.Economy.TryUse("bomb"), Is.False);
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);
            Assert.That(rig.Economy.TryUse("magnet"), Is.False, "a booster the config does not know");
            rig.Economy.Add("magnet", 5);
            Assert.That(rig.Economy.Count("magnet"), Is.Zero);
            rig.Economy.Add("bomb", -3);
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);
            rig.Economy.Add("bomb", 2);
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(2));
        }

        [Test]
        public void Free_supply_waits_its_hours()
        {
            var rig = new Rig();
            Assert.That(rig.Supply.FreeReady, Is.True, "the first free row is ready at once");

            Assert.That(rig.Supply.ClaimFree(), Is.True);
            Assert.That(rig.Economy.Coins, Is.EqualTo(15));
            Assert.That(rig.Supply.ClaimFree(), Is.False);
            Assert.That(rig.Supply.FreeReadyIn, Is.EqualTo(TimeSpan.FromHours(3)));

            rig.Clock.NowMillis += 3 * Hour - 1000;
            Assert.That(rig.Supply.FreeReady, Is.False);
            Assert.That(rig.Supply.FreeReadyIn, Is.EqualTo(TimeSpan.FromSeconds(1)));

            rig.Clock.NowMillis += 1000;
            Assert.That(rig.Supply.FreeReady, Is.True);
            Assert.That(rig.Supply.FreeReadyIn, Is.EqualTo(TimeSpan.Zero));
            Assert.That(rig.Supply.ClaimFree(), Is.True);
            Assert.That(rig.Economy.Coins, Is.EqualTo(30));
        }

        [Test]
        public void Ad_rows_unlock_in_order_and_reset_at_local_midnight()
        {
            var rig = new Rig();
            // 23:59 local
            rig.Clock.NowMillis = Noon + 12 * Hour - 60 * 1000;

            Assert.That(rig.Supply.NextAdRow, Is.Zero);
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(1, CancellationToken.None)), Is.False, "rows unlock in order");
            Assert.That(rig.Ads.Shows, Is.Zero, "no ad for a locked row");
            var paid = new List<int>();
            for (var row = 0; row < 4; row++)
            {
                var before = rig.Economy.Coins;
                Assert.That(Run(rig.Supply.ClaimAdRowAsync(row, CancellationToken.None)), Is.True);
                paid.Add(rig.Economy.Coins - before);
            }
            Assert.That(paid, Is.EqualTo(new[] { 20, 40, 50, 75 }));
            Assert.That(rig.Supply.NextAdRow, Is.EqualTo(-1), "today's ladder is done");
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.False);

            // 00:01 local, the next day (21:01 UTC: still the same UTC date)
            rig.Clock.NowMillis += 2 * 60 * 1000;
            Assert.That(rig.Supply.NextAdRow, Is.Zero);
        }

        [Test]
        public void Future_timestamp_restarts_the_timer()
        {
            var rig = new Rig();
            rig.Clock.NowMillis = Noon + 48 * Hour;
            Assert.That(rig.Supply.ClaimFree(), Is.True);
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.True);
            Assert.That(Run(rig.Wheel.SpinWithAdAsync(new Random(1), CancellationToken.None)), Is.Not.Null);
            Assert.That(rig.Login.Claim(), Is.Not.Null);
            var coins = rig.Economy.Coins;

            // The clock is turned back two days
            rig.Clock.NowMillis = Noon;
            Assert.That(rig.Supply.FreeReady, Is.False);
            Assert.That(rig.Supply.FreeReadyIn, Is.EqualTo(TimeSpan.FromHours(3)), "the timer restarts from now, never negative");
            Assert.That(rig.Supply.ClaimFree(), Is.False);
            Assert.That(rig.Supply.NextAdRow, Is.EqualTo(1), "the rows already used stay used");
            Assert.That(rig.Wheel.AdSpinsLeft, Is.EqualTo(2), "the spins already used stay used");
            Assert.That(rig.Login.ReadyToday, Is.False, "no second claim");
            Assert.That(rig.Login.Claim(), Is.Null);
            Assert.That(rig.Economy.Coins, Is.EqualTo(coins));

            rig.Clock.NowMillis += 3 * Hour;
            Assert.That(rig.Supply.FreeReady, Is.True);
            rig.Clock.NowMillis = Noon + 24 * Hour;
            Assert.That(rig.Login.Day, Is.EqualTo(1), "the streak restarts");
        }

        [Test]
        public void Wheel_free_spin_after_four_runs_or_four_won_levels()
        {
            var rig = new Rig();
            for (var i = 0; i < 3; i++) rig.Wheel.RunEnded(false, ShellMode.Endless);
            Assert.That(rig.Wheel.Progress, Is.EqualTo(3));
            Assert.That(rig.Wheel.FreeSpinReady, Is.False);
            Assert.That(rig.Wheel.Spin(new Random(1)), Is.Null);
            rig.Wheel.RunEnded(false, ShellMode.Endless);
            rig.Wheel.RunEnded(false, ShellMode.Endless);
            Assert.That(rig.Wheel.Progress, Is.EqualTo(4), "progress stops at freeEvery");
            Assert.That(rig.Wheel.FreeSpinReady, Is.True);

            var coins = rig.Economy.Coins;
            var boosters = rig.Economy.Count("bomb") + rig.Economy.Count("shield");
            var slice = rig.Wheel.Spin(new Random(1));
            Assert.That(slice, Is.Not.Null);
            Assert.That(rig.Economy.Coins - coins + rig.Economy.Count("bomb") + rig.Economy.Count("shield") - boosters,
                Is.EqualTo(slice.Coins + slice.Count), "the slice is paid");
            Assert.That(rig.Wheel.Progress, Is.Zero);
            Assert.That(rig.Ads.Shows, Is.Zero, "the free spin needs no ad");

            for (var i = 0; i < 4; i++) rig.Wheel.RunEnded(false, ShellMode.Levels);
            Assert.That(rig.Wheel.Progress, Is.Zero, "a failed level does not count");
            for (var i = 0; i < 4; i++) rig.Wheel.RunEnded(true, ShellMode.Levels);
            Assert.That(rig.Wheel.FreeSpinReady, Is.True);
        }

        [Test]
        public void Wheel_ad_spins_stop_at_three_a_day()
        {
            var rig = new Rig();
            var random = new Random(7);
            for (var i = 0; i < 3; i++) Assert.That(Run(rig.Wheel.SpinWithAdAsync(random, CancellationToken.None)), Is.Not.Null);

            Assert.That(rig.Wheel.AdSpinsLeft, Is.Zero);
            Assert.That(rig.Wheel.AdSpinReady, Is.False);
            Assert.That(Run(rig.Wheel.SpinWithAdAsync(random, CancellationToken.None)), Is.Null);
            Assert.That(rig.Ads.Shows, Is.EqualTo(3), "no fourth ad is shown");

            rig.Clock.NowMillis += 12 * Hour;
            Assert.That(rig.Wheel.AdSpinsLeft, Is.EqualTo(3), "a new local day");
        }

        [Test]
        public void Wheel_spin_follows_weights()
        {
            var rig = new Rig();
            var slices = rig.Wheel.Slices;
            var hits = new Dictionary<WheelSlice, int>();
            foreach (var slice in slices) hits[slice] = 0;
            var random = new Random(12345);
            const int spins = 10000;
            for (var i = 0; i < spins; i++)
            {
                for (var run = 0; run < 4; run++) rig.Wheel.RunEnded(false, ShellMode.Endless);
                hits[rig.Wheel.Spin(random)]++;
            }

            var total = 0;
            foreach (var slice in slices) total += slice.Weight;
            for (var i = 0; i < slices.Count; i++)
                Assert.That((double)hits[slices[i]] / spins, Is.EqualTo((double)slices[i].Weight / total).Within(0.02), $"slice {i}");
        }

        [Test]
        public void Login_resets_after_a_missed_day_and_wraps_after_seven()
        {
            var rig = new Rig();
            var days = new List<int>();
            for (var i = 0; i < 9; i++)
            {
                rig.Clock.NowMillis = Noon + i * 24 * Hour;
                Assert.That(rig.Login.ReadyToday, Is.True);
                days.Add(rig.Login.Day);
                rig.Login.Claim();
                Assert.That(rig.Login.ReadyToday, Is.False, "one claim a day");
                Assert.That(rig.Login.Claim(), Is.Null);
            }
            Assert.That(days, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 1, 2 }));
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(3), "day 7 pays its booster");

            // A day is missed
            rig.Clock.NowMillis = Noon + 10 * 24 * Hour;
            Assert.That(rig.Login.Day, Is.EqualTo(1));
            var coins = rig.Economy.Coins;
            Assert.That(rig.Login.Claim().Coins, Is.EqualTo(15));
            Assert.That(rig.Economy.Coins, Is.EqualTo(coins + 15));
        }

        [Test]
        public void Welcome_bundle_once_on_purchase_not_on_restore()
        {
            var rig = new Rig();
            var events = new FakePremiumPurchaseEvents();
            var bundle = new WelcomeBundle(rig.Economy, events);
            bundle.Initialize();
            Assert.That(bundle.Given, Is.False, "premium restored or found at launch raises no purchase");

            events.Purchase();
            events.Purchase();
            Assert.That(rig.Economy.Coins, Is.EqualTo(400));
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(5));

            // A restart, then another purchase event
            var economy = new Economy(EconomyConfig.Parse(Json), rig.Store);
            var again = new WelcomeBundle(economy, events);
            again.Initialize();
            bundle.Dispose();
            events.Purchase();
            Assert.That(again.Given, Is.True);
            Assert.That(economy.Coins, Is.EqualTo(400), "the save remembers the bundle");
            again.Dispose();
        }

        [Test]
        public void Unshown_ad_grants_nothing_and_counts_nothing()
        {
            var rig = new Rig();
            var bombs = rig.Economy.Count("bomb");
            var shields = rig.Economy.Count("shield");

            rig.Ads.Earns = false;
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.False);
            Assert.That(Run(rig.Wheel.SpinWithAdAsync(new Random(1), CancellationToken.None)), Is.Null);

            rig.Ads.Failure = new InvalidOperationException("ad failed");
            Assert.Throws<InvalidOperationException>(() => Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)));
            Assert.Throws<InvalidOperationException>(() => Run(rig.Wheel.SpinWithAdAsync(new Random(1), CancellationToken.None)));

            rig.Ads.Failure = new OperationCanceledException();
            Assert.Throws<OperationCanceledException>(() => Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)));

            rig.Ads.Failure = null;
            rig.Ads.RewardedAvailable = false;
            var shows = rig.Ads.Shows;
            Assert.That(rig.Supply.AdRowReady, Is.False);
            Assert.That(rig.Wheel.AdSpinReady, Is.False);
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.False);
            Assert.That(rig.Ads.Shows, Is.EqualTo(shows), "no ad is asked while none is ready");

            Assert.That(rig.Economy.Coins, Is.Zero);
            Assert.That(rig.Economy.Count("bomb"), Is.EqualTo(bombs));
            Assert.That(rig.Economy.Count("shield"), Is.EqualTo(shields));
            Assert.That(rig.Supply.NextAdRow, Is.Zero);
            Assert.That(rig.Wheel.AdSpinsLeft, Is.EqualTo(3));

            rig.Ads.RewardedAvailable = true;
            rig.Ads.Earns = true;
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.True, "a failed ad does not lock the row");
        }

        [Test]
        public void AutoPlay_keeps_every_window_closed()
        {
            var rig = new Rig { AutoPlay = true };
            for (var i = 0; i < 4; i++) rig.Wheel.RunEnded(true, ShellMode.Endless);

            Assert.That(rig.Supply.FreeReady, Is.False);
            Assert.That(rig.Supply.ClaimFree(), Is.False);
            Assert.That(rig.Supply.NextAdRow, Is.EqualTo(-1));
            Assert.That(Run(rig.Supply.ClaimAdRowAsync(0, CancellationToken.None)), Is.False);
            Assert.That(rig.Wheel.FreeSpinReady, Is.False);
            Assert.That(rig.Wheel.Spin(new Random(1)), Is.Null);
            Assert.That(rig.Wheel.AdSpinsLeft, Is.Zero);
            Assert.That(Run(rig.Wheel.SpinWithAdAsync(new Random(1), CancellationToken.None)), Is.Null);
            Assert.That(rig.Login.ReadyToday, Is.False);
            Assert.That(rig.Login.Claim(), Is.Null);
            Assert.That(rig.Ads.Shows, Is.Zero, "no ad is asked during an AutoPlay run");
            Assert.That(rig.Economy.Coins, Is.Zero);

            rig.AutoPlay = false;
            Assert.That(rig.Wheel.Progress, Is.Zero, "AutoPlay runs do not move the wheel");
            Assert.That(rig.Supply.FreeReady, Is.True);
            Assert.That(rig.Login.ReadyToday, Is.True);
        }

        // The helpers a custom extra is written with: a cooldown, a daily limit and a streak never pay twice when the
        // device's clock is turned back
        [Test]
        public void Cooldown_daily_counter_and_streak_survive_a_clock_turned_back()
        {
            var rig = new Rig(CustomJson);
            var values = rig.Economy.For("scratch");

            var cooldown = new Cooldown(values, "free", TimeSpan.FromHours(4), rig.Clock);
            Assert.That(cooldown.Ready, Is.True, "ready before the first start");
            Assert.That(cooldown.TryStart(), Is.True);
            Assert.That(cooldown.TryStart(), Is.False, "waiting");
            rig.Clock.NowMillis += Hour;
            Assert.That(cooldown.Remaining, Is.EqualTo(TimeSpan.FromHours(3)));
            rig.Clock.NowMillis -= 10 * Hour;
            Assert.That(cooldown.Ready, Is.False, "turned back: the wait restarts from now");
            Assert.That(cooldown.Remaining, Is.EqualTo(TimeSpan.FromHours(4)));
            rig.Clock.NowMillis += 4 * Hour;
            Assert.That(cooldown.TryStart(), Is.True, "the full wait later");

            rig.Clock.NowMillis = Noon;
            var daily = new DailyCounter(values, "cards", 2, rig.Clock, Zone);
            Assert.That((daily.TryUse(), daily.TryUse(), daily.TryUse()), Is.EqualTo((true, true, false)));
            Assert.That(daily.Left, Is.Zero);
            Assert.That(daily.ResetsIn, Is.EqualTo(TimeSpan.FromHours(12)), "local midnight at UTC+3");
            rig.Clock.NowMillis = Noon - 48 * Hour;
            Assert.That(daily.Left, Is.Zero, "turned back two days: today's uses stay used");
            Assert.That(daily.TryUse(), Is.False);
            rig.Clock.NowMillis = Noon + 24 * Hour;
            Assert.That(daily.Left, Is.EqualTo(2), "a new day");

            rig.Clock.NowMillis = Noon;
            var streak = new Streak(values, "days", 3, rig.Clock, Zone);
            Assert.That((streak.Day, streak.Claim(), streak.Claim()), Is.EqualTo((1, 1, 0)), "one claim a day");
            rig.Clock.NowMillis += 24 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(2));
            rig.Clock.NowMillis -= 72 * Hour;
            Assert.That(streak.ReadyToday, Is.False, "turned back: counts as claimed today");
            Assert.That(streak.Claim(), Is.Zero);
            rig.Clock.NowMillis += 24 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(1), "and starts again at 1 the next day");
            rig.Clock.NowMillis += 24 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(2));
            rig.Clock.NowMillis += 24 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(3));
            rig.Clock.NowMillis += 24 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(1), "the cycle starts again after its last day");
            rig.Clock.NowMillis += 48 * Hour;
            Assert.That(streak.Claim(), Is.EqualTo(1), "a missed day starts again at 1");

            var growing = new Streak(values, "run", 0, rig.Clock, Zone);
            for (var day = 1; day <= 9; day++, rig.Clock.NowMillis += 24 * Hour) Assert.That(growing.Claim(), Is.EqualTo(day));
        }

        [Test]
        public void Weighted_pick_reports_odds()
        {
            var weights = new[] { 30, 10, 0, 60 };
            Assert.That(WeightedPick.Odds(weights), Is.EqualTo(new[] { 0.3, 0.1, 0.0, 0.6 }).Within(1e-9));
            Assert.That(WeightedPick.Odds(new[] { 0, 0 }), Is.EqualTo(new[] { 0.0, 0.0 }));
            Assert.That(WeightedPick.Index(new[] { 0, -5 }, new Random(1)), Is.EqualTo(-1), "nothing to pick");

            var random = new Random(42);
            var counts = new int[weights.Length];
            const int rolls = 20000;
            for (var i = 0; i < rolls; i++) counts[WeightedPick.Index(weights, random)]++;
            Assert.That(counts[2], Is.Zero, "a weight of 0 is never picked");
            Assert.That(counts[0] / (double)rolls, Is.EqualTo(0.3).Within(0.02));
            Assert.That(counts[1] / (double)rolls, Is.EqualTo(0.1).Within(0.02));
            Assert.That(counts[3] / (double)rolls, Is.EqualTo(0.6).Within(0.02));

            var prizes = new[] { ("small", 3), ("big", 1) };
            Assert.That(WeightedPick.Odds(prizes, p => p.Item2), Is.EqualTo(new[] { 0.75, 0.25 }));
            Assert.That(new[] { "small", "big" }, Does.Contain(WeightedPick.Pick(prizes, p => p.Item2, new Random(3)).Item1));

            // The wheel picks with it: the same slice for the same seed as before
            var slices = EconomyConfig.Parse(Json).Wheel.Slices;
            Assert.That(Wheel.Pick(slices, new Random(7)), Is.SameAs(slices[WeightedPick.Index(slices.Select(x => x.Weight).ToList(), new Random(7))]));
        }

        [Test]
        public void Extras_store_keeps_values_per_id()
        {
            var rig = new Rig(CustomJson);
            var scratch = rig.Economy.For("scratch");
            var chest = rig.Economy.For("chest");
            Assert.That(scratch.Get("left"), Is.Zero, "0 when never set");
            Assert.That(scratch.GetText("state"), Is.Null);

            scratch.Set("left", 3);
            scratch.SetText("state", "{\"cells\":[1,0,2]}");
            chest.Set("left", 9);
            Assert.That((scratch.Get("left"), chest.Get("left")), Is.EqualTo((3L, 9L)), "each id has its own keys");
            Assert.That(chest.GetText("state"), Is.Null);
            Assert.That(rig.Store.Load().ExtraValues.Count, Is.EqualTo(3), "saved at once");

            // A new session over the same store reads them back; the wallet is untouched
            var again = new Economy(EconomyConfig.Parse(CustomJson), rig.Store);
            Assert.That(again.For("scratch").Get("left"), Is.EqualTo(3L));
            Assert.That(again.For("scratch").GetText("state"), Is.EqualTo("{\"cells\":[1,0,2]}"));
            Assert.That(again.Coins, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => again.For("scratch").Get(null));
        }
    }
}
