using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Features.Sample.Installer;
using Kturker.UnityBase.Features.Shell.GameShell;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.App.Game
{
    // Game scene: pause, guide and settings panels, the game shell (IGameShell: HUD, start hint, end cards) with the
    // economy's parts (IBoosters, the HUD's booster buttons and coins), then the feature modules; the generator adds one
    // line per cloned module inside the markers
    public sealed class GameInstaller : IInstaller
    {
        private readonly bool registerViews;

        public GameInstaller(bool registerViews) => this.registerViews = registerViews;

        public void Install(IContainerBuilder builder)
        {
            if (registerViews)
            {
                builder.RegisterComponentInHierarchy<SettingsView>().As<ISettingsView>();
                builder.RegisterComponentInHierarchy<PauseView>().As<IPauseView>();
                builder.RegisterComponentInHierarchy<GuideView>().As<IGuideView>();
            }
            builder.RegisterEntryPoint<SceneUiBinding>();
            builder.RegisterEntryPoint<SettingsPresenter>();
            builder.RegisterEntryPoint<PausePresenter>();
            builder.RegisterEntryPoint<GuidePresenter>(resolver => new GuidePresenter(resolver.Resolve<IGuideView>(),
                    resolver.Resolve<IPreferences>(), resolver.Resolve<IUiFeedback>(), enabled: Application.isEditor),
                Lifetime.Singleton);
            builder.RegisterEntryPoint<GameRenderMarker>();
            builder.RegisterEntryPoint<FrameTimeMarker>();
            new GameShellInstaller(registerViews).Install(builder);
            new EconomyGameInstaller(registerViews).Install(builder);
            // cmpose:features:start
            new SampleInstaller(registerViews).Install(builder);
            // cmpose:features:end
        }
    }
}
