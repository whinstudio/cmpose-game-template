using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Economy;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // The shop for the purchases flag in both project kinds (installed by RootInstaller's purchases block), and the
    // Remove Ads row in Settings while the game has ads. The catalog is the economy's (Remove Ads only without one).
    public sealed class ShopInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var adsOn = false;
            // cmpose:ads
            adsOn = true;
            // /cmpose:ads
            builder.Register<IShop>(resolver => new ShopHost(resolver.Resolve<IPurchases>(),
                resolver.TryResolve<IShopCatalog>(out var catalog) ? catalog : null, resolver.Resolve<IEconomy>(),
                resolver.Resolve<EconomyCurrency>(), resolver.Resolve<ILocalizer>(), adsOn, resolver.Resolve<IUiFeedback>(),
                resolver.Resolve<UiBinder>(), resolver.Resolve<ILog>(), () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            if (adsOn) builder.Register<ISettingsSection, PremiumSection>(Lifetime.Singleton);
        }
    }
}
