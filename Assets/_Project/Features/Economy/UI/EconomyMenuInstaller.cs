using Kturker.UnityBase.Core.Presentation;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy's part of the menu scope (MenuInstaller): the currency and the side buttons (IMenuSideButtons and
    // IMenuCoins come from the menu's view); IEconomyExtraButtons for the custom extras' buttons
    public sealed class EconomyMenuInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) =>
            builder.RegisterEntryPoint<EconomyMenu>(resolver => new EconomyMenu(resolver.Resolve<IMenuSideButtons>(), resolver.Resolve<IMenuCoins>(),
                resolver.Resolve<Economy>(), resolver.Resolve<Supply>(), resolver.Resolve<Wheel>(), resolver.Resolve<LoginBonus>(),
                resolver.Resolve<IEconomyWindows>(), resolver.Resolve<EconomyCurrency>(), resolver.Resolve<IUiFeedback>(), resolver.Resolve<IEconomyShop>()),
                Lifetime.Singleton);
    }
}
