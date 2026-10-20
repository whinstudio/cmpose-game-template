using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
            var flow = new AdsConsentFlow(gateway, tracking, ads, log, enabled, development, testIds, debugEea,
                automation: automation);
            flow.Start();
            return flow;
        }

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
            Assert.That(log.Lines, Has.Some.Contains("busy"));
        }
    }
}
