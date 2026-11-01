using Kturker.UnityBase.Core.Domain;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Insights
{
    // insights flag: IInsights over Firebase Crashlytics + Analytics, started at boot. The editor and automation runs
    // register the no-op gateway: the service reports available=false. IAnalyticsConsent comes from the root scope.
    public sealed class InsightsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var enabled = !Application.isEditor && !AutomationRun.Active;
            var releaseBuild = !Debug.isDebugBuild;
            builder.Register<IInsightsGateway>(resolver => InsightsGateways.Create(enabled, resolver.Resolve<ILog>()),
                Lifetime.Singleton);
            builder.Register<UnityLogSource>(Lifetime.Singleton).As<ILogSource>();
            builder.RegisterEntryPoint<Insights>(resolver => new Insights(resolver.Resolve<IInsightsGateway>(),
                    resolver.Resolve<IAnalyticsConsent>(), resolver.Resolve<ILog>(), enabled, releaseBuild,
                    resolver.Resolve<ILogSource>()), Lifetime.Singleton)
                .As<IInsights>();
        }
    }
}
