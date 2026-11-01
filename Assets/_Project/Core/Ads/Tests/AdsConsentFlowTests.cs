using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class AdsConsentFlowTests
    {
        private List<string> calls;
        private FakeGmaGateway gateway;
        private FakeTracking tracking;
        private FakeLog log;
        private GmaAds ads;

        [TearDown]
        public void ClearReservation()
        {
            SafeArea.ReservedTop = 0f;
            SafeArea.ReservedBottom = 0f;
        }

        [SetUp]
        public void Create()
        {
            calls = new List<string>();
            gateway = new FakeGmaGateway(calls);
            tracking = new FakeTracking(calls);
            log = new FakeLog();
            ads = new GmaAds(gateway, null, new InterstitialPacer(new FakeClock(), 0, 0, 1),
                new RewardedCap(new FakePreferences(), new FakeClock(), AdsConfig.RewardedDailyCap), new AdUnits("b", "i", "r"), log,
                (_, __) => UniTask.CompletedTask);
        }

        private AdsConsentFlow Start(bool enabled = true, bool development = false, bool testIds = false, bool debugEea = false,
            bool automation = false)
        {
            var flow = Create(enabled, development, testIds, debugEea, automation);
            flow.Start();
            return flow;
        }

        private AdsConsentFlow Create(bool enabled = true, bool development = false, bool testIds = false,
            bool debugEea = false, bool automation = false) =>
            new AdsConsentFlow(gateway, tracking, ads, log, enabled, development, testIds, debugEea, automation: automation);

        private string Marker => log.Lines.Last(l => l.StartsWith("MARKER CMPOSE_ADS "));

        [Test]
        public void Consent_then_tracking_then_the_sdk_in_that_order()
        {
            Start();

            Assert.That(calls, Is.EqualTo(new[] { "consentInfo", "consentForm", "att", "initialize" }));
            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(gateway.DebugEea, Is.False);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_ATT requested"));
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=true consent=notRequired att=authorized"));
        }

        [Test]
        public void NoAutomation_RunsTodaysOrder()
        {
            Start(automation: false);

            Assert.That(calls, Is.EqualTo(new[] { "consentInfo", "consentForm", "att", "initialize" }));
            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(log.Lines, Has.None.Contains("consent=automation"));
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=true consent=notRequired att=authorized"));
        }

        [Test]
        public void Automation_SkipsConsentTrackingAndAds()
        {
            tracking.Status = TrackingStatus.NotDetermined;

            var flow = Start(automation: true);

            Assert.That(calls, Is.Empty);
            Assert.That(tracking.Requests, Is.Zero);
            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_ADS consent=automation"));
            Assert.That(log.Lines, Has.None.EqualTo("MARKER CMPOSE_ATT requested"));
        }

        [Test]
        public void Automation_ShowOptionsDoesNothing()
        {
            gateway.PrivacyOptionsRequired = true;
            var flow = Start(automation: true);
            var changes = 0;
            flow.Changed += () => changes++;

            Assert.That(flow.OptionsRequired, Is.False);
            flow.ShowOptionsAsync().GetAwaiter().GetResult();

            Assert.That(calls, Is.Empty);
            Assert.That(changes, Is.Zero);
            Assert.That(ads.IsAvailable, Is.False);
        }

        [Test]
        public void Consent_given_in_the_form_is_reported_as_obtained()
        {
            gateway.CanRequestAds = false;
            gateway.Consent = ConsentState.Required;
            gateway.OnConsentForm = () =>
            {
                gateway.CanRequestAds = true;
                gateway.Consent = ConsentState.Obtained;
            };

            Start(debugEea: true);

            Assert.That(gateway.DebugEea, Is.True);
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=true consent=obtained att=authorized"));
        }

        [Test]
        public void Refused_consent_never_starts_the_sdk()
        {
            gateway.CanRequestAds = false;
            gateway.Consent = ConsentState.Required;

            Start();

            Assert.That(calls, Does.Not.Contain("initialize"));
            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=false consent=denied att=authorized"));
        }

        [Test]
        public void A_consent_error_keeps_ads_off_in_a_release_build()
        {
            gateway.ConsentInfoError = "offline";

            Start(development: false);

            Assert.That(calls, Is.EqualTo(new[] { "consentInfo", "att" }));
            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(log.Lines, Has.Some.Contains("offline"));
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=false consent=error att=authorized"));
        }

        [Test]
        public void A_consent_error_still_shows_test_ads_in_a_development_build()
        {
            gateway.ConsentFormError = "no form";

            Start(development: true);

            Assert.That(calls, Is.EqualTo(new[] { "consentInfo", "consentForm", "att", "initialize" }));
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=true consent=error att=authorized"));
        }

        [Test]
        public void Tracking_answered_before_is_not_asked_again()
        {
            tracking.Status = TrackingStatus.Denied;

            Start();

            Assert.That(tracking.Requests, Is.Zero);
            Assert.That(log.Lines, Has.None.EqualTo("MARKER CMPOSE_ATT requested"));
            Assert.That(Marker, Does.EndWith("att=denied"));
        }

        [Test]
        public void Android_has_no_tracking_prompt()
        {
            tracking.Status = TrackingStatus.NotApplicable;

            Start();

            Assert.That(calls, Is.EqualTo(new[] { "consentInfo", "consentForm", "initialize" }));
            Assert.That(Marker, Does.EndWith("att=notApplicable"));
        }

        [Test]
        public void The_editor_never_touches_the_sdks()
        {
            var flow = Start(enabled: false);

            Assert.That(calls, Is.Empty);
            Assert.That(flow.OptionsRequired, Is.False);
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=false consent=skipped att=notApplicable"));
        }

        // The menu asks for the banner before the flow ends; a run that ends without ads keeps no empty room for it
        [TestCase(false, false, null)]
        [TestCase(true, true, null)]
        [TestCase(true, false, "offline")]
        public void A_run_without_ads_gives_the_banner_room_back(bool enabled, bool automation, string consentError)
        {
            gateway.ExpectedBannerHeight = 168;
            gateway.ConsentInfoError = consentError;
            ads.ShowBanner();
            Assert.That(ads.BannerHeight, Is.EqualTo(168));

            Start(enabled: enabled, automation: automation);

            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(ads.BannerHeight, Is.Zero);
        }

        [Test]
        public void A_run_with_ads_keeps_the_banner_room()
        {
            gateway.ExpectedBannerHeight = 168;
            ads.ShowBanner();

            Start();

            Assert.That(ads.BannerHeight, Is.EqualTo(168));
        }

        [Test]
        public void Test_ids_in_a_release_build_log_a_warning()
        {
            Start(testIds: true);
            Assert.That(log.Lines, Has.Some.EqualTo("WARNING " + AdsConsentFlow.TestIdsWarning));

            log.Lines.Clear();
            Start(development: true, testIds: true);
            Assert.That(log.Lines, Has.None.EqualTo("WARNING " + AdsConsentFlow.TestIdsWarning));
        }

        [Test]
        public void Privacy_options_can_start_ads_later_and_start_the_sdk_once()
        {
            gateway.CanRequestAds = false;
            gateway.Consent = ConsentState.Required;
            gateway.PrivacyOptionsRequired = true;
            var flow = Start();
            var changes = 0;
            flow.Changed += () => changes++;
            gateway.OnPrivacyForm = () =>
            {
                gateway.CanRequestAds = true;
                gateway.Consent = ConsentState.Obtained;
            };

            Assert.That(flow.OptionsRequired, Is.True);
            flow.ShowOptionsAsync().GetAwaiter().GetResult();
            flow.ShowOptionsAsync().GetAwaiter().GetResult();

            Assert.That(calls.Count(c => c == "privacyForm"), Is.EqualTo(2));
            Assert.That(calls.Count(c => c == "initialize"), Is.EqualTo(1));
            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=true consent=obtained att=authorized"));
        }

        [Test]
        public void Withdrawn_consent_stops_ads()
        {
            var flow = Start();
            gateway.OnPrivacyForm = () =>
            {
                gateway.CanRequestAds = false;
                gateway.Consent = ConsentState.Required;
            };

            flow.ShowOptionsAsync().GetAwaiter().GetResult();

            Assert.That(ads.IsAvailable, Is.False);
            Assert.That(Marker, Is.EqualTo("MARKER CMPOSE_ADS available=false consent=denied att=authorized"));
        }

        [Test]
        public void A_privacy_form_error_changes_nothing()
        {
            var flow = Start();
            gateway.PrivacyFormError = "busy";
            gateway.OnPrivacyForm = () => gateway.CanRequestAds = false;

            flow.ShowOptionsAsync().GetAwaiter().GetResult();

            Assert.That(ads.IsAvailable, Is.True);
            Assert.That(flow.Granted, Is.True);
            Assert.That(log.Lines, Has.Some.Contains("busy"));
        }

        // Analytics has no answer until UMP gives one, then follows it; capture runs and the editor never allow it.
        // "refused": the player chose "Do not consent" where GDPR applies; UMP still allows non-personalised ads
        [TestCase("obtained", true)]
        [TestCase("refused", false)]
        [TestCase("notRequired", true)]
        [TestCase("denied", false)]
        [TestCase("errorInRelease", false)]
        [TestCase("errorInDevelopment", true)]
        [TestCase("automation", false)]
        [TestCase("disabled", false)]
        public void Analytics_consent_matches_each_ump_outcome(string outcome, bool granted)
        {
            string error = null;
            if (outcome == "obtained" || outcome == "refused")
            {
                gateway.Consent = ConsentState.Obtained;
                gateway.StorageConsent = outcome == "obtained";
            }
            if (outcome == "denied")
            {
                gateway.CanRequestAds = false;
                gateway.Consent = ConsentState.Required;
            }
            if (outcome.StartsWith("error")) error = "offline";
            gateway.PendingConsentInfo = new UniTaskCompletionSource<string>();
            var flow = Create(enabled: outcome != "disabled", development: outcome == "errorInDevelopment",
                automation: outcome == "automation");
            var changes = 0;
            flow.Changed += () => changes++;

            Assert.That(flow.Granted, Is.Null);
            flow.Start();
            if (outcome != "automation" && outcome != "disabled")
            {
                Assert.That(flow.Granted, Is.Null);
                Assert.That(changes, Is.Zero);
                gateway.PendingConsentInfo.TrySetResult(error);
            }

            Assert.That(flow.Granted, Is.EqualTo(granted));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Consent_withdrawn_in_the_privacy_options_turns_analytics_off()
        {
            var flow = Start();
            Assert.That(flow.Granted, Is.True);
            var changes = 0;
            flow.Changed += () => changes++;
            // As the SDK does: ads may still be requested (non-personalised), the TCF answer turns storage off
            gateway.OnPrivacyForm = () =>
            {
                gateway.Consent = ConsentState.Obtained;
                gateway.StorageConsent = false;
            };

            flow.ShowOptionsAsync().GetAwaiter().GetResult();

            Assert.That(flow.Granted, Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [TestCase(1, "1111111111", true)]
        [TestCase(1, "0111111111", false)]
        [TestCase(1, "", false)]
        [TestCase(1, null, false)]
        [TestCase(0, "0000000000", null)]
        [TestCase(0, "", null)]
        public void Tcf_purpose_one_decides_storage_where_gdpr_applies(int gdprApplies, string purposes, bool? expected)
        {
            Assert.That(TcfConsent.StorageConsent(gdprApplies, purposes), Is.EqualTo(expected));
        }
    }
}
