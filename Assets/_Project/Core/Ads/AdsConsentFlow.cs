using System;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Ads
{
    // Start-up of the ads flag, in Google's order and without holding up the boot: UMP consent info, the UMP form
    // when required, the ATT prompt on iOS when not asked yet, then Google Mobile Ads when consent allows ads. A UMP
    // error starts ads only in a development build. The editor touches neither SDK. Also the privacy options form,
    // after which ads start (once) or stop as consent now says. Compass's capture runs (flag file `compass-autoplay`)
    // skip consent and ads.
    public sealed class AdsConsentFlow : IStartable, IDisposable, IAdsPrivacy
    {
        public const string TestIdsWarning = "Ads use Google test IDs: replace them in AdsConfig before you ship";

        private readonly IGmaGateway gateway;
        private readonly ITrackingAuthorization tracking;
        private readonly GmaAds ads;
        private readonly ILog log;
        private readonly bool enabled;
        private readonly bool developmentBuild;
        private readonly bool testIds;
        private readonly bool debugEea;
        private readonly bool automation;
        private bool started;
        private bool busy;
        private bool disposed;
        private string consent = "skipped";
        private TrackingStatus att = TrackingStatus.NotApplicable;

        public AdsConsentFlow(IGmaGateway gateway, ITrackingAuthorization tracking, GmaAds ads, ILog log, bool enabled,
            bool developmentBuild, bool testIds, bool debugEea, bool automation)
        {
            this.gateway = gateway;
            this.tracking = tracking;
            this.ads = ads;
            this.log = log;
            this.enabled = enabled;
            this.developmentBuild = developmentBuild;
            this.testIds = testIds;
            this.debugEea = debugEea;
            this.automation = automation;
        }

        public bool OptionsRequired => enabled && !automation && gateway.PrivacyOptionsRequired;

        public event Action Changed;

        public void Start() => RunAsync().Forget();

        public void Dispose() => disposed = true;

        public async UniTask ShowOptionsAsync()
        {
            if (!enabled || automation || busy) return;
            busy = true;
            string error;
            try
            {
                error = await gateway.ShowPrivacyOptionsAsync();
            }
            finally
            {
                busy = false;
            }
            if (disposed) return;
            if (error != null)
            {
                log.Warning($"The privacy options failed: {error}");
                return;
            }
            await ApplyConsentAsync();
            if (disposed) return;
            Mark();
            Changed?.Invoke();
        }

        private async UniTaskVoid RunAsync()
        {
            if (!enabled)
            {
                Mark();
                return;
            }
            if (automation)
            {
                consent = "automation";
                log.Marker("CMPOSE_ADS consent=automation");
                Mark();
                return;
            }
            if (testIds && !developmentBuild) log.Warning(TestIdsWarning);
            var error = await gateway.RequestConsentInfoAsync(debugEea);
            if (error == null) error = await gateway.ShowConsentFormIfRequiredAsync();
            if (disposed) return;
            att = tracking.Status;
            if (att == TrackingStatus.NotDetermined)
            {
                log.Marker("CMPOSE_ATT requested");
                att = await tracking.RequestAsync();
                if (disposed) return;
            }
            if (error != null)
            {
                consent = "error";
                log.Warning(developmentBuild
                    ? $"Ad consent failed ({error}): a development build shows test ads anyway"
                    : $"Ad consent failed ({error}): no ads this run");
                if (developmentBuild) await StartAdsAsync();
            }
            else await ApplyConsentAsync();
            if (disposed) return;
            Mark();
            Changed?.Invoke();
        }

        // After UMP answered: start Google Mobile Ads when consent allows ads, stop showing them when it does not
        private async UniTask ApplyConsentAsync()
        {
            var canRequest = gateway.CanRequestAds;
            consent = !canRequest ? "denied" : gateway.Consent == ConsentState.NotRequired ? "notRequired" : "obtained";
            if (canRequest) await StartAdsAsync();
            else ads.Deactivate();
        }

        private async UniTask StartAdsAsync()
        {
            if (!started)
            {
                started = true;
                await gateway.InitializeAsync();
                if (disposed) return;
            }
            ads.Activate();
        }

        private void Mark()
        {
            var name = att.ToString();
            log.Marker($"CMPOSE_ADS available={(ads.IsAvailable ? "true" : "false")} consent={consent} " +
                $"att={char.ToLowerInvariant(name[0])}{name.Substring(1)}");
        }
    }
}
