using Kturker.UnityBase.App.Game;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Showcase.Installer;
using Kturker.UnityBase.Features.Showcase.Track3D;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.App.Track3D
{
    // The showcase's 3D track scene: pause and settings as in the other game scenes, the render marker, the track and
    // the showcase's shared parts
    public sealed class Track3DInstaller : IInstaller
    {
        private readonly bool registerViews;

        public Track3DInstaller(bool registerViews) => this.registerViews = registerViews;

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
            new Track3DGameInstaller(registerViews).Install(builder);
            new ShowcaseGameInstaller(registerViews).Install(builder);
        }
    }
}
