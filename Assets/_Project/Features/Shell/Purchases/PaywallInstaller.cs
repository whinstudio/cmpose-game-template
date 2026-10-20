using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // The paywall for the purchases flag in both project kinds (installed by RootInstaller's purchases block); the
    // economy's welcome bundle line when it registers one
    public sealed class PaywallInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) =>
            builder.Register<IPaywall>(resolver => new PaywallHost(resolver.Resolve<IPurchases>(), resolver.Resolve<AboutInfo>(),
                resolver.Resolve<IUrlOpener>(), resolver.Resolve<IUiFeedback>(), resolver.Resolve<UiBinder>(), resolver.Resolve<ILog>(),
                resolver.TryResolve<IPaywallBonus>(out var bonus) ? bonus : null, resolver.Resolve<ILocalizer>()), Lifetime.Singleton);
    }
}
