using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.Ads;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class MenuBannerTests
    {
        [Test]
        public void The_menu_shows_the_banner_and_every_other_scene_hides_it()
        {
            var loader = new FakeSceneLoader();
            var ads = new FakeAds();
            new MenuBanner(loader, ads).Start();

            loader.LoadAsync(SceneId.Menu);
            Assert.That(ads.BannerVisible, Is.True);
            loader.LoadAsync(SceneId.Game);

            Assert.That(ads.BannerVisible, Is.False);
            Assert.That(ads.BannerCalls, Is.EqualTo(new[] { "show", "hide" }));
        }

        // The device check: the menu drew frames without the banner's room after the loading overlay hid, then moved.
        // The room is right from the first visible frame: set on the menu, gone on the game, before the overlay hides.
        [Test]
        public void The_banner_room_is_set_before_the_loading_overlay_hides()
        {
            var gateway = new FakeGmaGateway { ExpectedBannerHeight = 168 };
            var ads = new GmaAds(gateway, null, new InterstitialPacer(new FakeClock(), 0, 0, 1),
                new RewardedCap(new FakePreferences(), new FakeClock(), AdsConfig.RewardedDailyCap), new AdUnits("b", "i", "r"),
                new FakeLog(), (_, __) => UniTask.CompletedTask);
            ads.Activate();
            var overlay = new FakeOverlay();
            var loader = new SceneLoader(new FakeGateway(), overlay, new FakeLog());
            var banner = new MenuBanner(loader, ads);
            banner.Start();
            var roomAtReveal = new List<float>();
            overlay.OnHide = () => roomAtReveal.Add(SafeArea.ReservedTop);
            try
            {
                loader.LoadAsync(SceneId.Menu).GetAwaiter().GetResult();
                loader.LoadAsync(SceneId.Game).GetAwaiter().GetResult();

                Assert.That(roomAtReveal, Is.EqualTo(new[] { 168f, 0f }));
            }
            finally
            {
                banner.Dispose();
                SafeArea.ReservedTop = 0f;
                SafeArea.ReservedBottom = 0f;
            }
        }

        [Test]
        public void After_dispose_scene_changes_do_nothing()
        {
            var loader = new FakeSceneLoader();
            var ads = new FakeAds();
            var banner = new MenuBanner(loader, ads);
            banner.Start();

            banner.Dispose();
            loader.LoadAsync(SceneId.Menu);

            Assert.That(ads.BannerCalls, Is.Empty);
        }
    }
}
