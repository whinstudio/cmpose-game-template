using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    // The windows' presenters over fake views: the booster offer and picker, the in-run uses, the supply, wheel and
    // login windows, the menu's badges and the HUD's coins
    public sealed class EconomyUiTests
    {
        // One inRun booster without stock, two preRun ones (one in stock)
        private const string Json =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\",\"icon\":\"Art/game-icons/missing-coin\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"icon\":\"Art/game-icons/missing-bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}," +
            "{\"id\":\"shield\",\"price\":120,\"offer\":\"preRun\",\"start\":1}," +
            "{\"id\":\"magnet\",\"price\":50,\"offer\":\"preRun\",\"start\":0}]," +
            "\"supply\":{\"freeCoins\":15,\"freeHours\":3,\"adCoins\":[20,40,50,75]}," +
            "\"wheel\":{\"freeEvery\":4,\"adSpinsPerDay\":3," +
            "\"slices\":[{\"coins\":15,\"weight\":30},{\"coins\":30,\"weight\":25},{\"coins\":60,\"weight\":15}," +
            "{\"coins\":150,\"weight\":5},{\"booster\":\"bomb\",\"count\":1,\"weight\":15},{\"booster\":\"shield\",\"count\":1,\"weight\":10}]}," +
            "\"login\":[{\"coins\":15},{\"coins\":15},{\"coins\":30},{\"coins\":30},{\"coins\":45},{\"coins\":45},{\"coins\":200,\"booster\":\"bomb\",\"count\":1}]," +
            "\"extras\":[{\"id\":\"daily\",\"kind\":\"supply\"},{\"id\":\"spin\",\"kind\":\"wheel\"},{\"id\":\"streak\",\"kind\":\"login\"}]," +
            "\"shop\":{\"kind\":\"simple\",\"rows\":[{\"id\":\"remove_ads\",\"usd\":4.99},{\"id\":\"coins_1\",\"usd\":0.99,\"coins\":160}]}}";

        private static readonly UnityEngine.Color Gold = new UnityEngine.Color(0xF9 / 255f, 0xB8 / 255f, 0x17 / 255f);

        private const long Hour = 3600L * 1000L;
        // 2026-10-20 12:00 at UTC+3 (09:00 UTC)
        private const long Noon = 1792486800000L;

        private static readonly TimeZoneInfo Zone =
            TimeZoneInfo.CreateCustomTimeZone("Test+03", TimeSpan.FromHours(3), "Test+03", "Test+03");

        private sealed class Rig
        {
            public readonly MemoryEconomyStore Store = new MemoryEconomyStore();
            public readonly FakeClock Clock = new FakeClock { NowMillis = Noon };
            public readonly FakeEconomyAds Ads = new FakeEconomyAds();
            public readonly FakeUiFeedback Feedback = new FakeUiFeedback();
            public readonly FakeLocalizer Localizer = new FakeLocalizer();
            public readonly ShellRunEvents Runs = new ShellRunEvents();
            public readonly PauseService Pause = new PauseService();
            public readonly FakeBoosterOfferView OfferView = new FakeBoosterOfferView();
            public readonly FakeBoosterPickerView PickerView = new FakeBoosterPickerView();
            public readonly FakeEconomyShop Shop = new FakeEconomyShop();
            public bool AutoPlay;
            public readonly Economy Economy;
            public readonly Supply Supply;
            public readonly Wheel Wheel;
            public readonly LoginBonus Login;
            public readonly EconomyCurrency Currency;
            public readonly BoosterOfferDialog Offer;
            public readonly BoosterPicker Picker;
            public readonly Boosters Boosters;

            public Rig(string json = Json)
            {
                Economy = new Economy(json == null ? null : EconomyConfig.Parse(json), Store);
                Supply = new Supply(Economy, Ads, Clock, () => AutoPlay, Zone);
                Wheel = new Wheel(Economy, Ads, Clock, () => AutoPlay, Zone);
                Login = new LoginBonus(Economy, Clock, () => AutoPlay, Zone);
                Currency = new EconomyCurrency(Economy.Config, Localizer);
                Offer = new BoosterOfferDialog(OfferView, Economy, Ads, Currency, Feedback, Shop, () => AutoPlay);
                Picker = new BoosterPicker(PickerView, Economy, Offer, Currency, Feedback);
                Boosters = new Boosters(Economy, Offer, Picker, Runs, Pause, () => AutoPlay);
            }

            public BoosterConfig Booster(string id) => Economy.Config.Booster(id);
        }

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        [Test]
        public void Offer_buys_with_coins_when_enough()
        {
            var rig = new Rig();
            rig.Economy.Earn(100);

            var offer = rig.Offer.OfferAsync(rig.Booster("bomb"), true, CancellationToken.None);

            Assert.That(rig.OfferView.Open, Is.True);
            Assert.That(rig.OfferView.NameKey, Is.EqualTo("economy.booster.bomb.name"));
            Assert.That(rig.OfferView.Icon, Is.Null, "a missing icon file draws the fallback letter");
            Assert.That((rig.OfferView.Price, rig.OfferView.Coins), Is.EqualTo((80, 100)));
            Assert.That(rig.OfferView.CanBuy, Is.True);
            Assert.That(rig.OfferView.Message, Is.Null);
            rig.OfferView.ClickBuy();
            Assert.That(Run(offer), Is.EqualTo(OfferResult.Bought));
            Assert.That(rig.Economy.Coins, Is.EqualTo(20));
            Assert.That(rig.OfferView.Open, Is.False);

            // 20 coins: the buy button is off, the ad still offers it
            offer = rig.Offer.OfferAsync(rig.Booster("bomb"), true, CancellationToken.None);
            Assert.That(rig.OfferView.CanBuy, Is.False);
            Assert.That(rig.OfferView.AdShown, Is.True);
            rig.OfferView.ClickBuy();
            Assert.That(rig.OfferView.Open, Is.True, "a press without the coins does nothing");
            Assert.That(rig.Economy.Coins, Is.EqualTo(20));
            rig.Economy.Earn(60);
            Assert.That(rig.OfferView.CanBuy, Is.True, "the button follows the wallet");
            rig.OfferView.ClickClose();
            Assert.That(Run(offer), Is.EqualTo(OfferResult.Closed));
            Assert.That(rig.Economy.Coins, Is.EqualTo(80));
        }

        [Test]
        public void Offer_hides_ad_button_when_no_rewarded_ad()
        {
            var rig = new Rig();
            rig.Ads.RewardedAvailable = false;

            var offer = rig.Offer.OfferAsync(rig.Booster("bomb"), true, CancellationToken.None);

            Assert.That(rig.OfferView.AdShown, Is.False);
            Assert.That(rig.OfferView.CanBuy, Is.False);
            Assert.That(rig.OfferView.Message, Is.EqualTo(EconomyKeys.CurrencyNotEnough));
            Assert.That(rig.OfferView.MessageArgs.Select(a => a.ToString()), Is.EqualTo(new[] { "Coins" }), "the name starts the line");
            rig.OfferView.ClickWatch();
            rig.OfferView.ClickBuy();
            Assert.That(rig.Ads.Shows, Is.Zero);
            Assert.That(rig.OfferView.Open, Is.True);
            rig.OfferView.ClickClose();
            Assert.That(Run(offer), Is.EqualTo(OfferResult.Closed));

            // An ad on offer, but this booster's ad of the run is used
            rig.Ads.RewardedAvailable = true;
            offer = rig.Offer.OfferAsync(rig.Booster("bomb"), false, CancellationToken.None);
            Assert.That(rig.OfferView.AdShown, Is.False);
            Assert.That(rig.OfferView.Message, Is.EqualTo(EconomyKeys.CurrencyNotEnough));
            rig.OfferView.ClickClose();
            Run(offer);
        }

        [Test]
        public void Ad_bought_booster_is_used_at_once_and_only_once_per_run()
        {
            var rig = new Rig();
            var used = new List<string>();
            rig.Boosters.Used += used.Add;

            var first = rig.Boosters.UseInRunAsync("bomb", CancellationToken.None);
            Assert.That(rig.Pause.Has(PauseReason.Window), Is.True, "the game waits under the offer");
            Assert.That(rig.OfferView.AdShown, Is.True);
            rig.OfferView.ClickWatch();

            Assert.That(Run(first), Is.True);
            Assert.That(used, Is.EqualTo(new[] { "bomb" }));
            Assert.That(rig.Economy.Count("bomb"), Is.Zero, "never stocked");
            Assert.That(rig.Pause.IsPaused, Is.False);

            var second = rig.Boosters.UseInRunAsync("bomb", CancellationToken.None);
            Assert.That(rig.OfferView.Open, Is.True);
            Assert.That(rig.OfferView.AdShown, Is.False, "one ad per booster a run");
            rig.OfferView.ClickClose();
            Assert.That(Run(second), Is.False);
            Assert.That(used.Count, Is.EqualTo(1));
            Assert.That(rig.Ads.Shows, Is.EqualTo(1));

            rig.Runs.Report(ShellMode.Endless, false);
            var next = rig.Boosters.UseInRunAsync("bomb", CancellationToken.None);
            Assert.That(rig.OfferView.AdShown, Is.True, "a new run offers the ad again");
            rig.OfferView.ClickClose();
            Run(next);

            // In stock: used without a window
            rig.Economy.Add("bomb", 1);
            var opened = rig.OfferView.Opened;
            Assert.That(Run(rig.Boosters.UseInRunAsync("bomb", CancellationToken.None)), Is.True);
            Assert.That(rig.OfferView.Opened, Is.EqualTo(opened));
            Assert.That(used.Count, Is.EqualTo(2));
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);
        }

        [Test]
        public void Unwatched_ad_gives_no_booster_and_says_it_was_not_ready()
        {
            var rig = new Rig();
            var used = new List<string>();
            rig.Boosters.Used += used.Add;
            rig.Ads.Earns = false;

            var use = rig.Boosters.UseInRunAsync("bomb", CancellationToken.None);
            rig.OfferView.ClickWatch();

            Assert.That(rig.OfferView.Open, Is.True);
            Assert.That(rig.OfferView.Message, Is.EqualTo(EconomyKeys.AdNotReady));
            Assert.That(used, Is.Empty);
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);
            rig.Ads.Earns = true;
            rig.OfferView.ClickWatch();
            Assert.That(Run(use), Is.True, "a later ad that was watched still pays");
            Assert.That(used, Is.EqualTo(new[] { "bomb" }));
        }

        [Test]
        public void Picker_returns_chosen_boosters_and_charges_them()
        {
            var rig = new Rig();
            rig.Economy.Earn(100);

            var choice = rig.Boosters.ChooseForRunAsync(CancellationToken.None);

            Assert.That(rig.PickerView.Open, Is.True);
            Assert.That(rig.PickerView.Rows.Select(r => r.NameKey), Is.EqualTo(new[] { "economy.booster.shield.name", "economy.booster.magnet.name" }),
                "only the preRun boosters");
            Assert.That(rig.PickerView.Rows[0].Owned, Is.EqualTo(1));
            rig.PickerView.ClickRow(0);
            Assert.That(rig.PickerView.Rows[0].Selected, Is.True);
            rig.PickerView.ClickRow(1);
            Assert.That(rig.OfferView.Open, Is.True, "none in stock: its offer opens");
            Assert.That(rig.OfferView.NameKey, Is.EqualTo("economy.booster.magnet.name"));
            rig.OfferView.ClickBuy();
            Assert.That(rig.PickerView.Rows[1].Selected, Is.True);
            Assert.That(rig.Economy.Coins, Is.EqualTo(50));
            rig.PickerView.ClickPlay();

            Assert.That(Run(choice), Is.EqualTo(new[] { "shield", "magnet" }));
            Assert.That(rig.Economy.Count("shield"), Is.Zero);
            Assert.That(rig.Economy.Count("magnet"), Is.Zero);
            Assert.That(rig.Economy.Coins, Is.EqualTo(50));
            Assert.That(rig.PickerView.Open, Is.False);

            // A tick taken back keeps the stock
            rig.Economy.Add("shield", 1);
            choice = rig.Boosters.ChooseForRunAsync(CancellationToken.None);
            rig.PickerView.ClickRow(0);
            rig.PickerView.ClickRow(0);
            rig.PickerView.ClickPlay();
            Assert.That(Run(choice), Is.Empty);
            Assert.That(rig.Economy.Count("shield"), Is.EqualTo(1));
        }

        [Test]
        public void Picker_ad_watched_booster_is_chosen_without_stock_or_coins()
        {
            var rig = new Rig();
            rig.Economy.Earn(100);

            var choice = rig.Boosters.ChooseForRunAsync(CancellationToken.None);

            rig.PickerView.ClickRow(1);
            Assert.That(rig.OfferView.Open, Is.True, "none in stock: its offer opens");
            Assert.That(rig.OfferView.AdShown, Is.True);
            rig.OfferView.ClickWatch();
            Assert.That(rig.OfferView.Open, Is.False);
            Assert.That(rig.Ads.Shows, Is.EqualTo(1));
            Assert.That(rig.PickerView.Rows[1].Selected, Is.True);
            Assert.That(rig.PickerView.Rows[1].ByAd, Is.True);
            Assert.That(rig.PickerView.Rows[1].Owned, Is.Zero, "never stocked");

            // The same run: the row is paid for, no second ad and no untick
            var opened = rig.OfferView.Opened;
            rig.PickerView.ClickRow(1);
            Assert.That(rig.OfferView.Opened, Is.EqualTo(opened), "one ad per booster a run");
            Assert.That(rig.PickerView.Rows[1].Selected, Is.True);
            rig.PickerView.ClickPlay();

            Assert.That(Run(choice), Is.EqualTo(new[] { "magnet" }));
            Assert.That(rig.Economy.Count("magnet"), Is.Zero);
            Assert.That(rig.Economy.Coins, Is.EqualTo(100));
            Assert.That(rig.Ads.Shows, Is.EqualTo(1));
        }

        [Test]
        public void Picker_is_skipped_under_autoplay()
        {
            var rig = new Rig { AutoPlay = true };
            rig.Economy.Earn(500);

            Assert.That(Run(rig.Boosters.ChooseForRunAsync(CancellationToken.None)), Is.Empty);
            Assert.That(Run(rig.Boosters.UseInRunAsync("bomb", CancellationToken.None)), Is.False);
            Assert.That(rig.PickerView.Opened, Is.Zero);
            Assert.That(rig.OfferView.Opened, Is.Zero);
            Assert.That(rig.Ads.Shows, Is.Zero);
            Assert.That(rig.Economy.Coins, Is.EqualTo(500));
            var supply = new SupplyWindow(new FakeSupplyWindowView(), rig.Economy, rig.Supply, rig.Ads, rig.Currency, rig.Feedback, () => rig.AutoPlay);
            Assert.That(supply.Open(), Is.False, "no window opens during an AutoPlay run");
            Assert.That(rig.Ads.BannerHeld, Is.False);
        }

        [Test]
        public void Without_economy_the_picker_and_the_bar_stay_away()
        {
            var rig = new Rig(null);
            var bar = new FakeBoosterBarView();
            new BoosterBar(bar, rig.Economy, rig.Boosters, rig.Feedback, () => false).Start();

            Assert.That(Run(rig.Boosters.ChooseForRunAsync(CancellationToken.None)), Is.Empty);
            Assert.That(rig.PickerView.Opened, Is.Zero);
            Assert.That(bar.Shown, Is.Empty);
        }

        [Test]
        public void Booster_bar_shows_the_in_run_boosters_and_their_stock()
        {
            var rig = new Rig();
            var bar = new FakeBoosterBarView();
            var used = new List<string>();
            rig.Boosters.Used += used.Add;
            new BoosterBar(bar, rig.Economy, rig.Boosters, rig.Feedback, () => false).Start();

            Assert.That(bar.Shown.Select(s => s.NameKey), Is.EqualTo(new[] { "economy.booster.bomb.name" }));
            Assert.That(bar.Counts[0], Is.Zero);
            rig.Economy.Add("bomb", 2);
            Assert.That(bar.Counts[0], Is.EqualTo(2));
            bar.Click(0);
            Assert.That(used, Is.EqualTo(new[] { "bomb" }));
            Assert.That(bar.Counts[0], Is.EqualTo(1));
        }

        // Two inRun boosters (bomb, freeze), or four: more than the bar's three slots
        private static readonly string TwoInRun = Json.Replace(
            "{\"id\":\"shield\"", "{\"id\":\"freeze\",\"price\":60,\"offer\":\"inRun\",\"start\":0},{\"id\":\"shield\"");
        private static readonly string FourInRun = Json.Replace(
            "{\"id\":\"shield\"", "{\"id\":\"freeze\",\"price\":60,\"offer\":\"inRun\",\"start\":0}," +
                                 "{\"id\":\"lantern\",\"price\":70,\"offer\":\"inRun\",\"start\":0}," +
                                 "{\"id\":\"drill\",\"price\":90,\"offer\":\"inRun\",\"start\":0},{\"id\":\"shield\"");

        [TestCase(2)]
        [TestCase(4)]
        public void Bar_under_autoplay_shows_in_run_boosters_with_demo_stock_and_ignores_clicks(int inRun)
        {
            var rig = new Rig(inRun == 2 ? TwoInRun : FourInRun) { AutoPlay = true };
            Assert.That(rig.Boosters.InRun.Count, Is.EqualTo(inRun));
            rig.Economy.Earn(500);
            var bar = new FakeBoosterBarView();
            var used = new List<string>();
            rig.Boosters.Used += used.Add;
            new BoosterBar(bar, rig.Economy, rig.Boosters, rig.Feedback, () => rig.AutoPlay).Start();

            var expected = Math.Min(inRun, bar.Slots);
            Assert.That(bar.Shown.Count, Is.EqualTo(expected), "the store shot shows the inRun boosters");
            Assert.That(bar.Shown[0].NameKey, Is.EqualTo("economy.booster.bomb.name"));
            Assert.That(BoosterBar.DemoStock, Is.EqualTo(3));
            Assert.That(bar.Counts, Is.EqualTo(Enumerable.Range(0, expected).ToDictionary(i => i, _ => 3)),
                "every badge shows the demo stock, never the \"+\" of an empty one");

            bar.Click(0);
            Assert.That(rig.OfferView.Opened, Is.Zero, "the AutoPlayer is never blocked by the offer");
            Assert.That(used, Is.Empty);
            Assert.That(rig.Economy.Coins, Is.EqualTo(500));
            Assert.That(rig.Economy.Count("bomb"), Is.Zero);

            rig.Economy.Add("bomb", 1);
            Assert.That(bar.Counts[0], Is.EqualTo(3), "the wallet does not overwrite the demo stock");
        }

        [Test]
        public void Supply_window_rows_follow_supply_state()
        {
            var rig = new Rig();
            var view = new FakeSupplyWindowView();
            var window = new SupplyWindow(view, rig.Economy, rig.Supply, rig.Ads, rig.Currency, rig.Feedback, () => rig.AutoPlay);

            Assert.That(window.Open(), Is.True);
            Assert.That(view.Open, Is.True);
            Assert.That(rig.Ads.BannerHeld, Is.True, "no banner over a window");
            Assert.That((view.FreeCoins, view.FreeReady), Is.EqualTo((15, true)));
            Assert.That((view.Currency.Icon, view.Currency.Colour, view.Currency.Letter), Is.EqualTo(((UnityEngine.Sprite)null, Gold, "C")),
                "no icon file: the fallback disc in the file's colour");
            Assert.That(view.Rows.Select(r => r.Coins), Is.EqualTo(new[] { 20, 40, 50, 75 }));
            Assert.That(view.Rows.Select(r => r.State), Is.EqualTo(new[]
                { SupplyRowState.Ready, SupplyRowState.Locked, SupplyRowState.Locked, SupplyRowState.Locked }));

            view.ClickFree();
            Assert.That(rig.Economy.Coins, Is.EqualTo(15));
            Assert.That(view.FreeReady, Is.False);
            Assert.That(view.FreeTimer, Is.EqualTo("3:00:00"));

            view.ClickAd(0);
            Assert.That(rig.Economy.Coins, Is.EqualTo(35));
            Assert.That(view.Rows.Select(r => r.State), Is.EqualTo(new[]
                { SupplyRowState.Claimed, SupplyRowState.Ready, SupplyRowState.Locked, SupplyRowState.Locked }));

            rig.Ads.Earns = false;
            view.ClickAd(1);
            Assert.That(view.Message, Is.EqualTo(EconomyKeys.AdNotReady));
            Assert.That(rig.Economy.Coins, Is.EqualTo(35), "an ad not watched pays nothing");
            Assert.That(view.Rows[1].State, Is.EqualTo(SupplyRowState.Ready));

            rig.Ads.RewardedAvailable = false;
            rig.Clock.NowMillis += 1000;
            window.Tick();
            Assert.That(view.Rows.Select(r => r.State), Is.EqualTo(new[]
                { SupplyRowState.Claimed, SupplyRowState.Locked, SupplyRowState.Locked, SupplyRowState.Locked }));
            Assert.That(view.FreeTimer, Is.EqualTo("2:59:59"));

            rig.Clock.NowMillis += 3 * Hour;
            window.Tick();
            Assert.That(view.FreeReady, Is.True);

            view.ClickClose();
            Assert.That(view.Open, Is.False);
            Assert.That(rig.Ads.BannerHeld, Is.False);

            // No ad on offer and none claimed today: the ad rows stay hidden
            var fresh = new Rig();
            fresh.Ads.RewardedAvailable = false;
            var freshView = new FakeSupplyWindowView();
            new SupplyWindow(freshView, fresh.Economy, fresh.Supply, fresh.Ads, fresh.Currency, fresh.Feedback, () => false).Open();
            Assert.That(freshView.Rows, Is.Empty);
        }

        [Test]
        public void Wheel_shows_odds_of_every_slice()
        {
            var rig = new Rig();
            var view = new FakeWheelWindowView();
            var window = new WheelWindow(view, rig.Economy, rig.Wheel, rig.Ads, rig.Localizer, rig.Feedback, () => rig.AutoPlay, new Random(7));

            Assert.That(window.Open(), Is.True);
            Assert.That(view.Slices.Count, Is.EqualTo(6));
            Assert.That(view.Slices[0].Amount, Is.EqualTo("15"));
            Assert.That(view.Slices[4].Amount, Is.EqualTo("economy.amount(1)"));
            Assert.That(view.Slices[4].LetterOf, Is.EqualTo("economy.booster.bomb.name"));
            Assert.That(view.Slices[0].Currency.Colour, Is.EqualTo(Gold), "the currency in its colour");
            Assert.That(view.Slices[4].Currency, Is.Null, "a booster in the slice's label colour");
            Assert.That(view.Odds, Is.Null);

            view.ClickInfo();
            Assert.That(view.Odds, Is.EqualTo(new[]
            {
                "economy.wheel.oddsLine(economy.currency.amount(15,coins),economy.wheel.percent(30))",
                "economy.wheel.oddsLine(economy.currency.amount(30,coins),economy.wheel.percent(25))",
                "economy.wheel.oddsLine(economy.currency.amount(60,coins),economy.wheel.percent(15))",
                "economy.wheel.oddsLine(economy.currency.amount(150,coins),economy.wheel.percent(5))",
                "economy.wheel.oddsLine(economy.reward.booster(1,economy.booster.bomb.name),economy.wheel.percent(15))",
                "economy.wheel.oddsLine(economy.reward.booster(1,economy.booster.shield.name),economy.wheel.percent(10))"
            }));
            view.ClickInfo();
            Assert.That(view.Odds, Is.Null);

            // Not free yet: the ad's spin with the spins left
            Assert.That(view.FreeReady, Is.False);
            Assert.That(view.AdShown, Is.True);
            Assert.That(view.Status, Is.EqualTo("economy.wheel.progress(0,4)"));
            Assert.That(view.Note, Is.EqualTo("economy.wheel.adLeft(3)"));
            for (var i = 0; i < 4; i++) rig.Wheel.RunEnded(false, ShellMode.Endless);
            window.Tick();
            Assert.That(view.FreeReady, Is.True);
            Assert.That(view.Status, Is.EqualTo(EconomyKeys.WheelReady));

            view.ClickSpin();
            Assert.That(view.SpunTo.Count, Is.EqualTo(1));
            Assert.That(view.Status, Does.StartWith("economy.wheel.won("));
            Assert.That(view.FreeReady, Is.False);
        }

        [Test]
        public void Wheel_ad_spin_that_was_not_watched_pays_nothing()
        {
            var rig = new Rig();
            var view = new FakeWheelWindowView();
            var window = new WheelWindow(view, rig.Economy, rig.Wheel, rig.Ads, rig.Localizer, rig.Feedback, () => false, new Random(7));
            window.Open();
            rig.Ads.Earns = false;

            view.ClickSpinAd();

            Assert.That(view.SpunTo, Is.Empty);
            Assert.That(view.Status, Is.EqualTo(EconomyKeys.AdNotReady));
            Assert.That(rig.Economy.Coins, Is.Zero);
            Assert.That(rig.Wheel.AdSpinsLeft, Is.EqualTo(3));
        }

        [Test]
        public void Login_window_marks_claimed_days()
        {
            var rig = new Rig();
            var view = new FakeLoginBonusView();
            var window = new LoginBonusWindow(view, rig.Economy, rig.Login, rig.Ads, rig.Localizer, rig.Feedback, () => rig.AutoPlay);

            Assert.That(window.Open(), Is.True);
            Assert.That(view.Days.Count, Is.EqualTo(7));
            Assert.That(view.Days.Select(d => d.State), Is.EqualTo(new[]
            {
                LoginDayState.Today, LoginDayState.Upcoming, LoginDayState.Upcoming, LoginDayState.Upcoming,
                LoginDayState.Upcoming, LoginDayState.Upcoming, LoginDayState.Upcoming
            }));
            Assert.That(view.Days[0].Currency.Colour, Is.EqualTo(Gold));
            Assert.That(view.Days[6].Prize, Is.EqualTo("economy.currency.amount(200,coins) + economy.reward.booster(1,economy.booster.bomb.name)"));
            Assert.That(view.ClaimReady, Is.True);

            view.ClickClaim();
            Assert.That(rig.Economy.Coins, Is.EqualTo(15));
            Assert.That(view.Days[0].State, Is.EqualTo(LoginDayState.Claimed));
            Assert.That(view.ClaimReady, Is.False);
            Assert.That(view.Next, Is.EqualTo("economy.login.next(12:00:00)"), "until local midnight");
            view.ClickClaim();
            Assert.That(rig.Economy.Coins, Is.EqualTo(15), "once a day");

            rig.Clock.NowMillis += 24 * Hour;
            window.Tick();
            Assert.That(view.Days[0].State, Is.EqualTo(LoginDayState.Claimed));
            Assert.That(view.Days[1].State, Is.EqualTo(LoginDayState.Today));

            // A missed day: the streak starts again at day 1
            rig.Clock.NowMillis += 48 * Hour;
            window.Tick();
            Assert.That(view.Days[0].State, Is.EqualTo(LoginDayState.Today));
            Assert.That(view.Days[1].State, Is.EqualTo(LoginDayState.Upcoming));
        }

        [Test]
        public void Menu_badges_follow_timers()
        {
            var rig = new Rig();
            var sides = new FakeMenuSideButtons();
            var windows = new FakeEconomyWindows();
            var menu = new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, windows, rig.Currency, rig.Feedback, rig.Shop);

            menu.Start();

            Assert.That(sides.Coins, Is.EqualTo(0));
            Assert.That((sides.CoinMark.Icon, sides.CoinMark.Letter), Is.EqualTo(((UnityEngine.Sprite)null, "C")), "no icon file: the fallback disc");
            Assert.That(sides.CoinMark.Colour, Is.EqualTo(Gold), "the file's colour");
            var supply = sides.Find(EconomyKeys.Supply);
            var wheel = sides.Find(EconomyKeys.Wheel);
            var login = sides.Find(EconomyKeys.Login);
            // The right column keeps two slots for the leaderboard and the achievements
            Assert.That((supply.Side, wheel.Side, login.Side), Is.EqualTo((MenuSide.Left, MenuSide.Right, MenuSide.Left)));
            Assert.That(supply.Icon, Is.SameAs(KitSprites.Gift));
            Assert.That(supply.Badge, Is.EqualTo(MenuBadge.Dot));
            Assert.That(wheel.Badge, Is.EqualTo(MenuBadge.Ad), "only an ad spin can be taken");
            Assert.That(login.Badge, Is.EqualTo(MenuBadge.Dot));

            rig.Supply.ClaimFree();
            Assert.That(supply.Badge, Is.EqualTo(MenuBadge.Ad), "the ad rows remain");
            Assert.That(sides.Coins, Is.EqualTo(15), "the coins follow the wallet");

            rig.Ads.RewardedAvailable = false;
            menu.Refresh();
            Assert.That((supply.Badge, supply.Timer), Is.EqualTo((MenuBadge.Timer, "3:00:00")));
            Assert.That(wheel.Badge, Is.EqualTo(MenuBadge.None));
            rig.Clock.NowMillis += Hour;
            menu.Refresh();
            Assert.That(supply.Timer, Is.EqualTo("2:00:00"));
            rig.Clock.NowMillis += 2 * Hour;
            menu.Refresh();
            Assert.That(supply.Badge, Is.EqualTo(MenuBadge.Dot));

            rig.Login.Claim();
            Assert.That((login.Badge, login.Timer), Is.EqualTo((MenuBadge.Timer, "9:00:00")), "15:00: nine hours to midnight");
            Assert.That(sides.Coins, Is.EqualTo(30));

            supply.Click();
            wheel.Click();
            login.Click();
            Assert.That(windows.Opened, Is.EqualTo(new[] { "supply", "wheel", "login" }));
        }

        [Test]
        public void Menu_shows_only_the_configured_features()
        {
            // The supply alone, in a made-up currency without an icon
            var rig = new Rig(EconomyTests.Without(Json, "wheel", "login")
                .Replace("{\"name\":\"coins\",\"color\":\"#F9B817\",\"icon\":\"Art/game-icons/missing-coin\"}", "{\"name\":\"sparks\",\"color\":\"#8E44AD\"}"));
            var sides = new FakeMenuSideButtons();
            var windows = new FakeEconomyWindows();
            var menu = new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, windows, rig.Currency, rig.Feedback, rig.Shop);

            menu.Start();

            Assert.That(sides.Buttons.Select(b => (b.LabelKey, b.Side)), Is.EqualTo(new[] { (EconomyKeys.Supply, MenuSide.Left) }));
            Assert.That(sides.Find(EconomyKeys.Supply).Badge, Is.EqualTo(MenuBadge.Dot));
            Assert.That((sides.CoinMark.Icon, sides.CoinMark.Letter), Is.EqualTo(((UnityEngine.Sprite)null, "S")));
            Assert.That(sides.CoinMark.Colour.b, Is.EqualTo(0xAD / 255f).Within(0.002f));
            menu.Refresh();
            menu.Tick();

            // The wheel and the login bonus: left Daily, right Spin, no Supply
            var two = new Rig(EconomyTests.Without(Json, "supply"));
            var twoSides = new FakeMenuSideButtons();
            new EconomyMenu(twoSides, twoSides, two.Economy, two.Supply, two.Wheel, two.Login, windows, two.Currency, two.Feedback, two.Shop).Start();
            Assert.That(twoSides.Buttons.Select(b => (b.LabelKey, b.Side)), Is.EqualTo(new[]
                { (EconomyKeys.Wheel, MenuSide.Right), (EconomyKeys.Login, MenuSide.Left) }));

            // A window of a feature the game does not have never opens
            Assert.That(new SupplyWindow(new FakeSupplyWindowView(), two.Economy, two.Supply, two.Ads, two.Currency, two.Feedback, () => false).Open(), Is.False);
            Assert.That(new WheelWindow(new FakeWheelWindowView(), rig.Economy, rig.Wheel, rig.Ads, rig.Localizer, rig.Feedback, () => false,
                new Random(1)).Open(), Is.False);
            Assert.That(new LoginBonusWindow(new FakeLoginBonusView(), rig.Economy, rig.Login, rig.Ads, rig.Localizer, rig.Feedback, () => false)
                .Open(), Is.False);
            Assert.That(rig.Ads.BannerHeld, Is.False);
        }

        [Test]
        public void No_features_leaves_only_the_currency_counter()
        {
            var rig = new Rig(EconomyTests.Without(Json, "supply", "wheel", "login", "extras").Replace("\"name\":\"coins\"", "\"name\":\"stars\""));
            var sides = new FakeMenuSideButtons();
            var menu = new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, new FakeEconomyWindows(), rig.Currency, rig.Feedback, rig.Shop);

            menu.Start();
            menu.Tick();

            Assert.That(sides.Buttons, Is.Empty);
            Assert.That(sides.Coins, Is.EqualTo(0));
            Assert.That(sides.CoinMark.Letter, Is.EqualTo("S"));
            rig.Economy.Earn(7);
            Assert.That(sides.Coins, Is.EqualTo(7), "the counter follows the wallet");
            menu.Dispose();
            rig.Economy.Earn(1);
            Assert.That(sides.Coins, Is.EqualTo(7));

            // The offer still sells boosters for the currency
            var offer = rig.Offer.OfferAsync(rig.Booster("magnet"), false, CancellationToken.None);
            Assert.That(rig.OfferView.Currency.Name, Is.EqualTo("stars"));
            Assert.That(rig.OfferView.Message, Is.EqualTo("economy.currency.notEnough"));
            Assert.That(rig.OfferView.MessageArgs[0].ToString(), Is.EqualTo("Stars"));
            rig.OfferView.ClickClose();
            Run(offer);
        }

        [Test]
        public void Menu_without_economy_shows_no_coins_and_no_buttons()
        {
            var rig = new Rig(null);
            var sides = new FakeMenuSideButtons();
            new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, new FakeEconomyWindows(), rig.Currency, rig.Feedback, rig.Shop).Start();

            Assert.That(sides.Coins, Is.Null);
            Assert.That(sides.CoinWrites, Is.EqualTo(1));
            Assert.That(sides.Buttons, Is.Empty);
        }

        private static GameShell Shell(FakeShellHudView hud) => new GameShell(hud, new FakeStartHintView(), new FakeGameOverCard(),
            new FakeLevelCompleteCard(), new FakeLevelFailedCard(), new FakeLevelProgress(), new FakeShellAds(), new FakeUiFeedback(),
            new FakeLog(), false);

        [Test]
        public void Hud_counter_follows_the_wallet()
        {
            var rig = new Rig();
            var view = new FakeShellHudView();
            var shell = Shell(view);
            var hud = new EconomyHud(shell, rig.Economy, rig.Currency);
            hud.Start();

            shell.Begin(ShellMode.Endless);
            Assert.That(view.Counter, Is.EqualTo(0));
            rig.Economy.Earn(25);
            Assert.That(view.Counter, Is.EqualTo(25));
            shell.Begin(ShellMode.Endless);
            Assert.That(view.Counter, Is.EqualTo(25), "a new run shows the coins again");
            hud.Dispose();
            rig.Economy.Earn(5);
            Assert.That(view.Counter, Is.EqualTo(25));

            // No economy: the counter stays the game's
            var off = new Rig(null);
            var offView = new FakeShellHudView();
            var offShell = Shell(offView);
            new EconomyHud(offShell, off.Economy, off.Currency).Start();
            offShell.Begin(ShellMode.Endless);
            Assert.That(offView.Counter, Is.Null);
            Assert.That(offView.CounterIcon, Is.Null, "the plain dot");
        }

        [Test]
        public void Timer_text_counts_whole_seconds_up()
        {
            Assert.That(EconomyText.Timer(TimeSpan.FromHours(3)), Is.EqualTo("3:00:00"));
            Assert.That(EconomyText.Timer(TimeSpan.FromSeconds(59.2)), Is.EqualTo("0:01:00"));
            Assert.That(EconomyText.Timer(TimeSpan.FromSeconds(-5)), Is.EqualTo("0:00:00"));
        }

        // A localizer over a small table: a missing key reads as the key (TableLocalizer)
        private sealed class TableStub : Kturker.UnityBase.Core.Domain.ILocalizer
        {
            private readonly Dictionary<string, string> table;

            public TableStub(Dictionary<string, string> table, string language = "en")
            {
                this.table = table;
                Language = language;
            }

            public string Language { get; set; }
            public event Action Changed;

            public string Get(string key, params object[] args)
            {
                if (!table.TryGetValue(key, out var text)) return key;
                return args == null || args.Length == 0 ? text : string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args);
            }

            public void Set(string key, string text, string language)
            {
                table[key] = text;
                Language = language;
                Changed?.Invoke();
            }
        }

        [Test]
        public void Currency_name_comes_from_the_table_then_the_file()
        {
            var config = EconomyConfig.Parse(Json.Replace("\"name\":\"coins\"", "\"name\":\"acorns\""));
            var table = new TableStub(new Dictionary<string, string>
            {
                [EconomyKeys.CurrencyAmount] = "{0} {1}",
                [EconomyKeys.CurrencyNotEnough] = "{0}: not enough"
            });
            var currency = new EconomyCurrency(config, table);
            Assert.That(currency.Name, Is.EqualTo("acorns"), "not in the tables: the file's name");
            Assert.That(currency.Amount(80), Is.EqualTo("80 acorns"));
            Assert.That(currency.Mark.Letter, Is.EqualTo("A"));

            table.Set(EconomyKeys.CurrencyName, "ıhlamur", "tr");
            Assert.That(currency.Name, Is.EqualTo("ıhlamur"), "the game's table names it");
            Assert.That(currency.Amount(80), Is.EqualTo("80 ıhlamur"));
            Assert.That(table.Get(EconomyKeys.CurrencyNotEnough, currency.CapitalNameArg), Is.EqualTo("Ihlamur: not enough"), "Turkish casing");
            table.Set(EconomyKeys.CurrencyName, "istiridye", "tr");
            Assert.That(currency.CapitalNameArg.ToString(), Is.EqualTo("İstiridye"));
            Assert.That(currency.Mark.Letter, Is.EqualTo("I"), "the mark's letter follows the language's name");
            table.Set(EconomyKeys.CurrencyName, "  ", "en");
            Assert.That(currency.Name, Is.EqualTo("acorns"), "a blank name in a table falls back to the file");

            // A text bound once follows a change of language: the name is read when the text is written
            var line = string.Format("{0}", currency.NameArg);
            table.Set(EconomyKeys.CurrencyName, "glands", "fr");
            Assert.That((line, currency.NameArg.ToString()), Is.EqualTo(("acorns", "glands")));

            var off = new EconomyCurrency(null, table);
            Assert.That((off.Name, off.Mark), Is.EqualTo((string.Empty, (CurrencyMark)null)), "nothing with the economy off");
        }

        [Test]
        public void Hud_counter_shows_the_currency_icon()
        {
            var rig = new Rig();
            var view = new FakeShellHudView();
            var shell = Shell(view);
            new EconomyHud(shell, rig.Economy, rig.Currency).Start();
            shell.Begin(ShellMode.Endless);

            Assert.That(view.CounterIcon, Is.Not.Null, "the currency's mark, not the plain dot");
            Assert.That((view.CounterIcon.Icon, view.CounterIcon.Colour, view.CounterIcon.Letter),
                Is.EqualTo(((UnityEngine.Sprite)null, Gold, "C")), "no icon file: the fallback disc in its colour");

            // The shell passes it to its HUD as it is; null brings back the plain dot
            shell.SetCounterIcon(null);
            Assert.That(view.CounterIcon, Is.Null);
        }

        // A custom extra: its button after the ready ones and its content in the frame, which keeps its rules: never
        // during an AutoPlay run, never for an id that is not one of the file's custom extras, banner hidden while open
        [Test]
        public void Custom_extra_gets_a_menu_button_and_the_window_frame()
        {
            var rig = new Rig(EconomyTests.Without(Json, "login").Replace("\"extras\":[", "\"extras\":[{\"id\":\"scratch\",\"kind\":\"custom\"},"));
            var sides = new FakeMenuSideButtons();
            var windows = new FakeEconomyWindows();
            var menu = new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, windows, rig.Currency, rig.Feedback, rig.Shop);
            var opened = 0;

            var button = menu.AddExtraButton("scratch", KitSprites.Gift, () => opened++);
            menu.Start();
            Assert.That(sides.Buttons.Select(b => (b.LabelKey, b.Side)), Is.EqualTo(new[]
            {
                (EconomyKeys.Supply, MenuSide.Left), (EconomyKeys.Wheel, MenuSide.Right), ("economy.extra.scratch.name", MenuSide.Left)
            }), "the ready extras first, in the file's order; the custom one where there is room");
            ((FakeMenuSideButton)button).Click();
            Assert.That((opened, rig.Feedback.Clicks), Is.EqualTo((1, 1)));
            Assert.That(menu.AddExtraButton("daily", null, null), Is.Null, "a ready extra is not custom");
            Assert.That(menu.AddExtraButton("chest", null, null), Is.Null, "not in the file");

            var view = new FakeExtraWindowView();
            var frame = new ExtraWindow(view, rig.Economy, rig.Ads, rig.Feedback, () => rig.AutoPlay, null);
            var content = new FakeExtraContent("scratch");
            Assert.That(frame.Open(new FakeExtraContent("daily")), Is.False, "only a custom extra");
            Assert.That(frame.Open(new FakeExtraContent("chest")), Is.False);
            rig.AutoPlay = true;
            Assert.That(frame.Open(content), Is.False, "never during an AutoPlay run");
            Assert.That((content.Shown, view.Open, rig.Ads.BannerHeld), Is.EqualTo((0, false, false)));
            rig.AutoPlay = false;

            Assert.That(frame.Open(content), Is.True);
            Assert.That((view.Open, view.Title, content.Shown, rig.Ads.BannerHeld), Is.EqualTo((true, "economy.extra.scratch.name", 1, true)));
            Assert.That(content.Ticks, Is.EqualTo(1), "filled once as it opens");
            Assert.That(frame.Open(new FakeExtraContent("scratch")), Is.False, "one content at a time");
            frame.Tick();
            Assert.That(content.Ticks, Is.EqualTo(2));

            // An ad in progress: Close waits and the ticks stop
            content.Frame.Busy = true;
            Assert.That(view.Busy, Is.True);
            view.ClickClose();
            frame.Tick();
            Assert.That((view.Open, content.Ticks), Is.EqualTo((true, 2)));
            content.Frame.Busy = false;
            var token = content.Frame.Token;
            content.Frame.Close();
            Assert.That((view.Open, content.Hidden, rig.Ads.BannerHeld, token.IsCancellationRequested), Is.EqualTo((false, 1, false, true)));

            Assert.That(frame.Open(content), Is.True, "opens again");
            view.ClickClose();
            Assert.That((view.Open, content.Hidden), Is.EqualTo((false, 2)));

            // Without an economy nothing opens
            var off = new Rig(null);
            Assert.That(new ExtraWindow(new FakeExtraWindowView(), off.Economy, off.Ads, off.Feedback, () => false, null).Open(content), Is.False);
        }

        [Test]
        public void Menu_coins_open_the_shop_while_it_can_show()
        {
            var rig = new Rig();
            var sides = new FakeMenuSideButtons();
            var menu = new EconomyMenu(sides, sides, rig.Economy, rig.Supply, rig.Wheel, rig.Login, new FakeEconomyWindows(), rig.Currency,
                rig.Feedback, rig.Shop);
            menu.Start();

            sides.ClickCoins();
            Assert.That(rig.Shop.Shown, Is.EqualTo(new[] { false }), "from the counter, not the not-enough moment");
            Assert.That(rig.Feedback.Clicks, Is.EqualTo(1));

            rig.Shop.CanShow = false;
            sides.ClickCoins();
            menu.Dispose();
            rig.Shop.CanShow = true;
            sides.ClickCoins();
            Assert.That(rig.Shop.Shown.Count, Is.EqualTo(1));
        }
    }
}
