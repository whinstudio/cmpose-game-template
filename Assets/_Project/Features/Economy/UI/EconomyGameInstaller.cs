using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy's part of a game scene with the game shell (after GameShellInstaller): IBoosters for the game's
    // code, the inRun booster buttons (BoosterBarView in the shell's HUD) and the currency in the HUD's counter.
    // registerViews=false lets tests supply a fake bar.
    public sealed class EconomyGameInstaller : IInstaller
    {
        private readonly bool registerViews;

        public EconomyGameInstaller(bool registerViews) => this.registerViews = registerViews;

        public void Install(IContainerBuilder builder)
        {
            if (registerViews) builder.RegisterComponentInHierarchy<BoosterBarView>().As<IBoosterBarView>();
            builder.Register(resolver => new Boosters(resolver.Resolve<IEconomy>(), resolver.Resolve<IBoosterOffer>(),
                    resolver.Resolve<IBoosterPicker>(), resolver.Resolve<ShellRunEvents>(), resolver.Resolve<PauseService>(),
                    () => AutoPlaySwitch.Requested), Lifetime.Scoped)
                .As<IBoosters>().AsSelf();
            builder.RegisterEntryPoint<BoosterBar>(resolver => new BoosterBar(resolver.Resolve<IBoosterBarView>(), resolver.Resolve<IEconomy>(),
                resolver.Resolve<Boosters>(), resolver.Resolve<IUiFeedback>(), () => AutoPlaySwitch.Requested), Lifetime.Singleton);
            builder.RegisterEntryPoint<EconomyHud>(resolver => new EconomyHud(resolver.Resolve<GameShell>(), resolver.Resolve<IEconomy>(),
                    resolver.Resolve<EconomyCurrency>()),
                Lifetime.Singleton);
        }
    }
}
