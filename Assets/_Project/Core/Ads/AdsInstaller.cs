using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Update;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Ads
{
    // ads flag: IAds over Google Mobile Ads, started by AdsConsentFlow after UMP (and ATT on iOS), never in the
    // editor. Premium comes from IPremiumStatus when a flag registers one (purchases); otherwise nobody is premium.
    public sealed class AdsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var units = AdsConfig.Units(Application.platform == RuntimePlatform.IPhonePlayer);
            var development = Debug.isDebugBuild;
            builder.Register<IGmaGateway, GmaGateway>(Lifetime.Singleton);
            builder.Register<ITrackingAuthorization>(_ => TrackingAuthorizations.ForPlatform(), Lifetime.Singleton);
            builder.Register<GmaAds>(resolver => new GmaAds(resolver.Resolve<IGmaGateway>(), Premium(resolver),
                    new InterstitialPacer(resolver.Resolve<IClock>(), AdsConfig.InterstitialStartDelaySeconds * 1000L,
                        AdsConfig.InterstitialGapSeconds * 1000L, AdsConfig.InterstitialEvery),
                    units, resolver.Resolve<ILog>(), GmaAds.RealDelay), Lifetime.Singleton)
                .As<IAds>().AsSelf();
            builder.RegisterEntryPoint<AdsConsentFlow>(resolver => new AdsConsentFlow(resolver.Resolve<IGmaGateway>(),
                    resolver.Resolve<ITrackingAuthorization>(), resolver.Resolve<GmaAds>(), resolver.Resolve<ILog>(),
                    enabled: !Application.isEditor, developmentBuild: development, testIds: AdsConfig.UsesTestIds(units),
                    debugEea: development && AdsConfig.DebugGeographyEea, automation: AutomationRun.Active),
                Lifetime.Singleton)
                .AsSelf();
            builder.Register<ISettingsSection, PrivacySection>(Lifetime.Singleton);
        }

        private static IPremiumStatus Premium(IObjectResolver resolver) =>
            resolver.TryResolve<IPremiumStatus>(out var premium) ? premium : null;
    }
}
