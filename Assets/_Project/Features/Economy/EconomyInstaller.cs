using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Update;
using VContainer;
using VContainer.Unity;
// cmpose:saveSystem
using Kturker.UnityBase.Core.Save;
// /cmpose:saveSystem
// cmpose:ads
using Kturker.UnityBase.Core.Ads;
// /cmpose:ads
// cmpose:purchases
using Kturker.UnityBase.Core.Purchases;
// /cmpose:purchases

namespace Kturker.UnityBase.Features.Economy
{
    // The economy in the root scope (off without Resources/GameEconomy.json). Each flag swaps in its part: the save
    // store keeps the wallet, the ads unlock the ad rows and spins, the purchases pay the shop's rows (ShopGrants) and
    // open the shop from the currency counter and the booster offer (IEconomyShop).
    // Needs ShellRunEvents and IClock from the root installer. The windows (EconomyWindows) live here too; the menu and
    // the Game scene add their parts with EconomyMenuInstaller and EconomyGameInstaller.
    public sealed class EconomyInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IEconomyStore, MemoryEconomyStore>(Lifetime.Singleton);
            builder.Register<IEconomyAds, NoEconomyAds>(Lifetime.Singleton);
            builder.Register<IEconomyShop, NoEconomyShop>(Lifetime.Singleton);
            // cmpose:saveSystem
            builder.Register<IEconomyStore>(resolver => new SavedEconomyStore(resolver.Resolve<ISaveStore>()), Lifetime.Singleton);
            // /cmpose:saveSystem
            // cmpose:ads
            builder.Register<IEconomyAds>(resolver => new RewardedEconomyAds(resolver.Resolve<IAds>(), resolver.Resolve<ILog>(),
                () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            // /cmpose:ads
            // cmpose:purchases
            builder.Register<IEconomyShop>(resolver => new PurchasesEconomyShop(resolver.Resolve<IShop>()), Lifetime.Singleton);
            builder.RegisterEntryPoint<ShopGrants>(resolver =>
            {
                var clock = resolver.Resolve<IClock>();
                return new ShopGrants(resolver.Resolve<Economy>(), resolver.Resolve<IPurchases>(), resolver.Resolve<EconomyShopCatalog>(),
                    () => DateTimeOffset.FromUnixTimeMilliseconds(clock.NowMillis).UtcDateTime);
            }, Lifetime.Singleton).AsSelf();
            // /cmpose:purchases
            builder.Register(resolver => new Economy(EconomyConfig.Current, resolver.Resolve<IEconomyStore>()), Lifetime.Singleton)
                .As<IEconomy>().As<IEconomyExtrasStore>().AsSelf();
            builder.Register(resolver => new EconomyShopCatalog(resolver.Resolve<Economy>().Config), Lifetime.Singleton)
                .As<IShopCatalog>().AsSelf();
            builder.Register(resolver => new EconomyCurrency(resolver.Resolve<Economy>().Config, resolver.Resolve<ILocalizer>()), Lifetime.Singleton);
            builder.Register(resolver => new Supply(resolver.Resolve<Economy>(), resolver.Resolve<IEconomyAds>(),
                resolver.Resolve<IClock>(), () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            builder.Register(resolver => new Wheel(resolver.Resolve<Economy>(), resolver.Resolve<IEconomyAds>(),
                resolver.Resolve<IClock>(), () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            builder.Register(resolver => new LoginBonus(resolver.Resolve<Economy>(), resolver.Resolve<IClock>(),
                () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            builder.RegisterEntryPoint<WheelRunCounter>();
            // RegisterEntryPoint registers every implemented interface: IEconomyWindows, IBoosterOffer, IBoosterPicker
            builder.RegisterEntryPoint<EconomyWindows>(resolver => new EconomyWindows(resolver.Resolve<Economy>(), resolver.Resolve<Supply>(),
                resolver.Resolve<Wheel>(), resolver.Resolve<LoginBonus>(), resolver.Resolve<IEconomyAds>(), resolver.Resolve<ILocalizer>(),
                resolver.Resolve<IUiFeedback>(), resolver.Resolve<UiBinder>(), resolver.Resolve<ILog>(), resolver.Resolve<IEconomyShop>(),
                () => AutoPlaySwitch.Requested), Lifetime.Singleton).AsSelf();
        }
    }
}
