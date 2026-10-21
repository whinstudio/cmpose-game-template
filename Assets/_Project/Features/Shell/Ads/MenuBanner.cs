using System;
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Navigation;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Shell.Ads
{
    // The menu's banner: at the top of the menu, hidden in every other scene (the game, its end panel and the windows
    // never show one). Asking for it keeps its room at once (IAds.BannerHeight, SafeArea keeps the menu below it), so
    // the menu does not move when the ad loads. It asks (or lets go) on ISceneLoader.Revealing, while the loading
    // overlay still covers the new scene, so its first visible frame already has the right room.
    public sealed class MenuBanner : IStartable, IDisposable
    {
        private readonly ISceneLoader loader;
        private readonly IAds ads;

        public MenuBanner(ISceneLoader loader, IAds ads)
        {
            this.loader = loader;
            this.ads = ads;
        }

        public void Start() => loader.Revealing += OnRevealing;

        public void Dispose() => loader.Revealing -= OnRevealing;

        private void OnRevealing(SceneId scene)
        {
            if (scene == SceneId.Menu) ads.ShowBanner();
            else ads.HideBanner();
        }
    }
}
