using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class GmaAdsTests
    {
        private static readonly AdUnits Units = new AdUnits("banner-unit", "interstitial-unit", "rewarded-unit");

        private FakeGmaGateway gateway;
        private FakePremium premium;
        private FakeLog log;
        private List<int> waits;
        private FakePreferences preferences;
        private FakeClock clock;

        [SetUp]
        public void Create()
        {
            gateway = new FakeGmaGateway();
            premium = new FakePremium();
            log = new FakeLog();
            waits = new List<int>();
            preferences = new FakePreferences();
            // 2026-10-20 12:00 UTC
            clock = new FakeClock { NowMillis = 1792497600000L };
            SafeArea.ReservedTop = 0f;
            SafeArea.ReservedBottom = 0f;
        }

        [TearDown]
        public void ClearReservation()
        {
            SafeArea.ReservedTop = 0f;
            SafeArea.ReservedBottom = 0f;
        }

        private RewardedCap Cap() => new RewardedCap(preferences, clock, AdsConfig.RewardedDailyCap, TimeZoneInfo.Utc);

        private GmaAds Make(int every = 1) =>
            new GmaAds(gateway, premium, new InterstitialPacer(new FakeClock(), 0, 0, every), Cap(), Units, log, (seconds, _) =>
            {
                waits.Add(seconds);
                return UniTask.CompletedTask;
            });

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        private IEnumerable<string> Markers => log.Lines.Where(l => l.StartsWith("MARKER CMPOSE_AD "));

        [Test]
        public void Unavailable_ads_never_touch_the_sdk()
        {
            var ads = Make();
            ads.ShowBanner();

            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.NotReady));
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            Assert.That(gateway.InterstitialLoads + gateway.RewardedLoads + gateway.InterstitialShows + gateway.RewardedShows, Is.Zero);
            Assert.That(gateway.Banners, Is.Empty);
            Assert.That(Markers, Is.EqualTo(new[]
            {
                "MARKER CMPOSE_AD format=interstitial result=NotReady",
                "MARKER CMPOSE_AD format=rewarded result=NotReady"
            }));
        }

        [Test]
        public void Activation_preloads_both_formats_and_shows_a_banner_asked_for_earlier()
        {
            var ads = Make();
            var changes = 0;
            ads.Changed += () => changes++;
            ads.ShowBanner();

            ads.Activate();
            gateway.FinishBanner(true);

            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(gateway.InterstitialLoads, Is.EqualTo(1));
            Assert.That(gateway.RewardedLoads, Is.EqualTo(1));
            Assert.That(gateway.LoadedUnits, Is.EquivalentTo(new[] { "interstitial-unit", "rewarded-unit" }));
            Assert.That(gateway.Banners, Is.EqualTo(new[] { "banner-unit" }));
            Assert.That(Markers, Is.EqualTo(new[] { "MARKER CMPOSE_AD format=banner result=Shown" }));
        }

        [Test]
        public void Premium_skips_interstitials_and_never_shows_a_banner()
        {
            premium.IsPremium = true;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();

            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Skipped));
            Assert.That(gateway.InterstitialLoads + gateway.InterstitialShows, Is.Zero);
            Assert.That(gateway.Banners, Is.Empty);
            Assert.That(ads.BannerHeight, Is.Zero);
        }

        [Test]
        public void Premium_still_sees_opt_in_rewarded_ads()
        {
            premium.IsPremium = true;
            var off = Make();
            Assert.That(Run(off.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            var ads = Make();
            ads.Activate();

            Assert.That(ads.RewardedAvailable, Is.True);
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Rewarded));
            Assert.That(gateway.RewardedShows, Is.EqualTo(1));
            Assert.That(gateway.RewardedLoads, Is.EqualTo(2));
            Assert.That(Cap().CountToday, Is.EqualTo(1));
        }

        [Test]
        public void Premium_earns_economy_rewards_until_the_cap()
        {
            premium.IsPremium = true;
            var ads = Make();
            ads.Activate();
            var economy = new RewardedEconomyAds(ads, log, () => false);

            for (var i = 0; i < 15; i++)
                Assert.That(Run(economy.ShowRewardedAsync(CancellationToken.None)), Is.True, $"ad {i + 1}");

            Assert.That(economy.RewardedAvailable, Is.False);
            Assert.That(Run(economy.ShowRewardedAsync(CancellationToken.None)), Is.False);
            Assert.That(gateway.RewardedShows, Is.EqualTo(15));
        }

        [Test]
        public void Rewarded_cap_stops_at_fifteen_a_day()
        {
            var ads = Make();
            ads.Activate();
            var changes = 0;
            ads.Changed += () => changes++;

            for (var i = 0; i < 15; i++)
            {
                Assert.That(ads.RewardedAvailable, Is.True, $"ad {i + 1}");
                Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Rewarded));
            }

            Assert.That(ads.RewardedAvailable, Is.False);
            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            Assert.That(gateway.RewardedShows, Is.EqualTo(15));
            // A new GmaAds (the next launch) reads the same count
            var relaunched = Make();
            relaunched.Activate();
            Assert.That(relaunched.RewardedAvailable, Is.False);
        }

        [Test]
        public void Cap_counts_only_shown_rewarded_ads()
        {
            gateway.RewardedLoadSucceeds = false;
            var ads = Make();
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            ads.Activate();
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            gateway.RewardedLoadSucceeds = true;
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            foreach (var reply in new[] { RewardResult.Dismissed, RewardResult.Failed })
            {
                gateway.RewardedReply = reply;
                Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(reply));
            }

            Assert.That(gateway.RewardedShows, Is.EqualTo(2));
            Assert.That(Cap().CountToday, Is.Zero);
            Assert.That(preferences.Values, Is.Empty);
            gateway.RewardedReply = RewardResult.Rewarded;
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Rewarded));
            Assert.That(Cap().CountToday, Is.EqualTo(1));
        }

        [Test]
        public void Cap_resets_at_local_midnight()
        {
            // UTC+3: 2026-10-20 23:59 local is 20:59 UTC
            var zone = TimeZoneInfo.CreateCustomTimeZone("Plus3", TimeSpan.FromHours(3), "Plus3", "Plus3");
            clock.NowMillis = new DateTimeOffset(2026, 10, 20, 23, 59, 0, TimeSpan.FromHours(3)).ToUnixTimeMilliseconds();
            var cap = new RewardedCap(preferences, clock, 15, zone);
            for (var i = 0; i < 15; i++) cap.Count();
            Assert.That(cap.Reached, Is.True);

            clock.NowMillis += 60_000;

            Assert.That(cap.Reached, Is.False);
            Assert.That(cap.CountToday, Is.Zero);
            cap.Count();
            Assert.That(cap.CountToday, Is.EqualTo(1));
            Assert.That(preferences.GetString(RewardedCap.DateKey, null), Is.EqualTo("2026-10-21"));
            Assert.That(preferences.Saves, Is.EqualTo(16));
        }

        [Test]
        public void A_clock_turned_back_frees_no_extra_rewarded_ads()
        {
            var cap = Cap();
            for (var i = 0; i < 14; i++) cap.Count();
            var now = clock.NowMillis;

            clock.NowMillis = now - 3L * 24 * 60 * 60 * 1000;
            Assert.That(cap.CountToday, Is.EqualTo(14));
            cap.Count();
            Assert.That(cap.Reached, Is.True);
            clock.NowMillis = now;
            Assert.That(cap.Reached, Is.True);
            Assert.That(preferences.GetString(RewardedCap.DateKey, null), Is.EqualTo("2026-10-20"));

            clock.NowMillis = long.MinValue;
            Assert.That(cap.Reached, Is.True);
            clock.NowMillis = now + 24L * 60 * 60 * 1000;
            Assert.That(cap.Reached, Is.False);
        }

        [Test]
        public void Banner_is_requested_at_the_top()
        {
            var ads = Make();
            ads.ShowBanner();
            ads.Activate();

            Assert.That(gateway.BannerPositions, Is.EqualTo(new[] { BannerPosition.Top }));
        }

        // The device check: the paywall's Close became Continue when the content moved down under the finger as the
        // banner arrived. The room is kept from the moment the banner is asked for.
        [Test]
        public void Banner_room_is_kept_before_the_banner_loads_and_does_not_change_when_it_loads()
        {
            gateway.ExpectedBannerHeight = 168;
            gateway.BannerHeight = 168;
            var ads = Make();
            ads.Activate();

            ads.ShowBanner();
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
            Assert.That(ads.BannerHeight, Is.EqualTo(168));

            gateway.FinishBanner(true);
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
            Assert.That(SafeArea.ReservedBottom, Is.Zero);
            Assert.That(ads.BannerHeight, Is.EqualTo(168));
        }

        // First launch: the menu (and the first paywall over it) opens before UMP has answered
        [Test]
        public void Banner_room_is_kept_while_consent_has_not_answered_yet()
        {
            gateway.ExpectedBannerHeight = 168;
            var ads = Make();

            ads.ShowBanner();
            Assert.That(gateway.Banners, Is.Empty);
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));

            ads.Activate();
            gateway.FinishBanner(true);
            Assert.That(gateway.Banners, Is.EqualTo(new[] { "banner-unit" }));
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
        }

        [Test]
        public void A_banner_that_fails_to_load_keeps_its_room()
        {
            gateway.ExpectedBannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();

            gateway.FinishBanner(false);

            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
        }

        [Test]
        public void A_loaded_banner_of_another_height_takes_its_own_room()
        {
            gateway.ExpectedBannerHeight = 160;
            gateway.BannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();

            gateway.FinishBanner(true);

            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
            Assert.That(ads.BannerHeight, Is.EqualTo(168));
        }

        [Test]
        public void Turning_premium_gives_the_banner_room_back_at_once()
        {
            gateway.ExpectedBannerHeight = 168;
            gateway.BannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();
            gateway.FinishBanner(true);

            premium.IsPremium = true;
            premium.Raise();

            Assert.That(gateway.BannerHides, Is.EqualTo(1));
            Assert.That(SafeArea.ReservedTop, Is.Zero);
            Assert.That(ads.BannerHeight, Is.Zero);
        }

        [Test]
        public void Premium_players_get_no_banner_room()
        {
            gateway.ExpectedBannerHeight = 168;
            premium.IsPremium = true;
            var ads = Make();

            ads.ShowBanner();
            Assert.That(SafeArea.ReservedTop, Is.Zero);
            ads.Activate();
            Assert.That(SafeArea.ReservedTop, Is.Zero);
        }

        [Test]
        public void Ads_off_for_this_run_give_the_banner_room_back()
        {
            gateway.ExpectedBannerHeight = 168;
            var ads = Make();
            ads.ShowBanner();
            var changes = 0;
            ads.Changed += () => changes++;

            ads.Deactivate();
            Assert.That(SafeArea.ReservedTop, Is.Zero);
            Assert.That(changes, Is.Zero);

            ads.HideBanner();
            ads.ShowBanner();
            Assert.That(SafeArea.ReservedTop, Is.Zero);
        }

        [Test]
        public void Withdrawn_consent_gives_the_banner_room_back()
        {
            gateway.ExpectedBannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();

            ads.Deactivate();

            Assert.That(SafeArea.ReservedTop, Is.Zero);
        }

        // An economy window over the menu: the banner goes while it is open, its room stays, so neither the window's
        // centred card nor the menu behind it moves when it opens or closes
        [Test]
        public void Holding_the_banner_hides_it_and_keeps_its_room()
        {
            gateway.ExpectedBannerHeight = 168;
            gateway.BannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();
            gateway.FinishBanner(true);

            ads.HoldBanner(true);
            Assert.That(gateway.BannerHides, Is.EqualTo(1));
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
            Assert.That(ads.BannerHeight, Is.EqualTo(168));

            ads.HoldBanner(false);
            Assert.That(gateway.Banners, Is.EqualTo(new[] { "banner-unit", "banner-unit" }));
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
        }

        [Test]
        public void A_held_banner_still_gives_its_room_back_when_the_player_turns_premium()
        {
            gateway.ExpectedBannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();
            ads.HoldBanner(true);

            premium.IsPremium = true;
            premium.Raise();
            ads.HoldBanner(false);

            Assert.That(SafeArea.ReservedTop, Is.Zero);
            Assert.That(gateway.Banners.Count, Is.EqualTo(1));
        }

        // Another scene (the game) asks the banner away
        [Test]
        public void Hiding_the_banner_gives_its_room_back_and_showing_it_again_keeps_it_at_once()
        {
            gateway.ExpectedBannerHeight = 160;
            gateway.BannerHeight = 168;
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();
            gateway.FinishBanner(true);

            ads.HideBanner();
            Assert.That(SafeArea.ReservedTop, Is.Zero);
            Assert.That(ads.BannerHeight, Is.Zero);

            gateway.ExpectedBannerHeight = 168;
            ads.ShowBanner();
            Assert.That(SafeArea.ReservedTop, Is.EqualTo(168f));
        }

        [Test]
        public void Turning_premium_hides_the_banner_at_once_and_losing_it_brings_the_banner_back()
        {
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();

            premium.IsPremium = true;
            premium.Raise();
            Assert.That(gateway.BannerHides, Is.EqualTo(1));
            Assert.That(gateway.RewardedLoads, Is.EqualTo(1));

            premium.IsPremium = false;
            premium.Raise();
            Assert.That(gateway.Banners, Is.EqualTo(new[] { "banner-unit", "banner-unit" }));
        }

        [Test]
        public void A_shown_interstitial_is_loaded_again()
        {
            var ads = Make();
            ads.Activate();

            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Shown));
            Assert.That(gateway.InterstitialShows, Is.EqualTo(1));
            Assert.That(gateway.InterstitialLoads, Is.EqualTo(2));
            Assert.That(Markers.Last(), Is.EqualTo("MARKER CMPOSE_AD format=interstitial result=Shown"));
        }

        [Test]
        public void Rewarded_reports_a_reward_a_dismissal_or_a_failure_and_loads_again()
        {
            var ads = Make();
            ads.Activate();

            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Rewarded));
            gateway.RewardedReply = RewardResult.Dismissed;
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Dismissed));
            gateway.RewardedReply = RewardResult.Failed;
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.Failed));
            Assert.That(gateway.RewardedShows, Is.EqualTo(3));
            Assert.That(gateway.RewardedLoads, Is.EqualTo(4));
        }

        [Test]
        public void A_second_request_while_an_ad_is_open_is_refused()
        {
            var ads = Make();
            ads.Activate();
            gateway.PendingInterstitial = new UniTaskCompletionSource<AdResult>();

            var first = ads.ShowInterstitialAsync(CancellationToken.None);
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.NotReady));
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
            gateway.PendingInterstitial.TrySetResult(AdResult.Shown);

            Assert.That(Run(first), Is.EqualTo(AdResult.Shown));
            Assert.That(gateway.InterstitialShows, Is.EqualTo(1));
            Assert.That(gateway.RewardedShows, Is.Zero);
        }

        [Test]
        public void A_failed_load_retries_three_times_with_growing_waits_then_waits_for_the_next_request()
        {
            gateway.InterstitialLoadSucceeds = false;
            var ads = Make();
            ads.Activate();

            Assert.That(gateway.InterstitialLoads, Is.EqualTo(4));
            Assert.That(waits, Is.EqualTo(new[] { 2, 4, 8 }));
            Assert.That(log.Lines, Has.Some.StartsWith("WARNING No interstitial ad after 4 tries"));

            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.NotReady));
            Assert.That(gateway.InterstitialLoads, Is.EqualTo(8));
            gateway.InterstitialLoadSucceeds = true;
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.NotReady));
            Assert.That(gateway.InterstitialLoads, Is.EqualTo(9));
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Shown));
        }

        [Test]
        public void The_pacer_decides_which_interstitial_requests_show()
        {
            var ads = Make(every: 2);
            ads.Activate();

            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Skipped));
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Shown));
            Assert.That(Run(ads.ShowInterstitialAsync(CancellationToken.None)), Is.EqualTo(AdResult.Skipped));
            Assert.That(gateway.InterstitialShows, Is.EqualTo(1));
        }

        [Test]
        public void Deactivation_hides_the_banner_and_refuses_ads()
        {
            var ads = Make();
            ads.Activate();
            ads.ShowBanner();
            var changes = 0;
            ads.Changed += () => changes++;

            ads.Deactivate();

            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(gateway.BannerHides, Is.EqualTo(1));
            Assert.That(Run(ads.ShowRewardedAsync(CancellationToken.None)), Is.EqualTo(RewardResult.NotReady));
        }

        [Test]
        public void Hiding_a_banner_that_is_not_shown_does_nothing()
        {
            var ads = Make();
            ads.Activate();

            ads.HideBanner();
            Assert.That(gateway.BannerHides, Is.Zero);
            ads.ShowBanner();
            ads.ShowBanner();
            ads.HideBanner();

            Assert.That(gateway.Banners.Count, Is.EqualTo(1));
            Assert.That(gateway.BannerHides, Is.EqualTo(1));
        }
    }
}
