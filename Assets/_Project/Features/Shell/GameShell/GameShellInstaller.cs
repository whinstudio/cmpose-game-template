using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// cmpose:ads
using Kturker.UnityBase.Core.Ads;
// /cmpose:ads

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The shell in a game scene (its views are in the scene; ILevelProgress comes from the root scope);
    // registerViews=false lets tests supply fake views. The editor is strict: a level flow without a save store throws.
    public sealed class GameShellInstaller : IInstaller
    {
        private readonly bool registerViews;

        public GameShellInstaller(bool registerViews) => this.registerViews = registerViews;

        public void Install(IContainerBuilder builder)
        {
            if (registerViews)
            {
                builder.RegisterComponentInHierarchy<ShellHudView>().As<IShellHudView>();
                builder.RegisterComponentInHierarchy<StartHintView>().As<IStartHintView>();
                builder.RegisterComponentInHierarchy<GameOverCard>().As<IGameOverCard>();
                builder.RegisterComponentInHierarchy<LevelCompleteCard>().As<ILevelCompleteCard>();
                builder.RegisterComponentInHierarchy<LevelFailedCard>().As<ILevelFailedCard>();
            }
            builder.Register<IShellAds, NoShellAds>(Lifetime.Scoped);
            // cmpose:ads
            builder.Register<IShellAds>(resolver => new RewardedShellAds(resolver.Resolve<IAds>(),
                resolver.TryResolve<IPremiumStatus>(out var premium) ? premium : null), Lifetime.Scoped);
            // /cmpose:ads
            builder.Register(resolver => new GameShell(resolver.Resolve<IShellHudView>(), resolver.Resolve<IStartHintView>(),
                    resolver.Resolve<IGameOverCard>(), resolver.Resolve<ILevelCompleteCard>(), resolver.Resolve<ILevelFailedCard>(),
                    resolver.Resolve<ILevelProgress>(), resolver.Resolve<IShellAds>(), resolver.Resolve<IUiFeedback>(),
                    resolver.Resolve<ILog>(), Application.isEditor,
                    resolver.TryResolve<ShellRunEvents>(out var runs) ? runs : null), Lifetime.Scoped)
                .As<IGameShell>().AsSelf();
        }
    }
}
