using Kturker.UnityBase.App.Game;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Showcase.Installer;
using Kturker.UnityBase.Features.Showcase.Runner2D;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.App.Runner2D
{
    // The showcase's 2D runner scene: pause and settings as in the sample's Game scene, the render marker, the runner
    // and the showcase's shared parts
    public sealed class Runner2DInstaller : IInstaller
    {
        private readonly bool registerViews;

        public Runner2DInstaller(bool registerViews) => this.registerViews = registerViews;

        public void Install(IContainerBuilder builder)
        {
            if (registerViews)
            {
                builder.RegisterComponentInHierarchy<SettingsView>().As<ISettingsView>();
                builder.RegisterComponentInHierarchy<PauseView>().As<IPauseView>();
            }
            builder.RegisterEntryPoint<SceneUiBinding>();
            builder.RegisterEntryPoint<SettingsPresenter>();
            builder.RegisterEntryPoint<PausePresenter>();
            builder.RegisterEntryPoint<GameRenderMarker>();
            builder.RegisterEntryPoint<FrameTimeMarker>();
            new Runner2DGameInstaller(registerViews).Install(builder);
            new ShowcaseGameInstaller(registerViews).Install(builder);
        }
    }
}
