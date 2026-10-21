using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Network;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.NetworkInspector
{
    // networkInspector flag (requires network): records IApiClient's exchanges, shows them in development builds
    // except Compass's capture runs (the iOS simulator player is always a development build, and the NET button
    // must not appear in store shots or the video)
    public sealed class InspectorInstaller : IInstaller
    {
        public static bool ShowInspector(bool debugBuild, bool automation) => debugBuild && !automation;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<InspectorObserver>(Lifetime.Singleton).As<IRequestObserver>().AsSelf();
            var show = ShowInspector(Debug.isDebugBuild, AutomationRun.Active);
            builder.RegisterEntryPoint<InspectorOverlayHost>(resolver => new InspectorOverlayHost(resolver.Resolve<InspectorObserver>(),
                resolver.Resolve<UiBinder>(), resolver.Resolve<ILog>(), show), Lifetime.Singleton);
        }
    }
}
