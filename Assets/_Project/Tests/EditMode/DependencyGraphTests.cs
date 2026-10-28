using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.About;
using Kturker.UnityBase.App.Boot;
using Kturker.UnityBase.App.Game;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.App.Update;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.DeepLink;
using Kturker.UnityBase.Core.DI;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;
using Kturker.UnityBase.Core.Update;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Features.Shell.GameShell;
// cmpose:sample
using Kturker.UnityBase.Features.Sample.Presentation;
// /cmpose:sample
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// cmpose:saveSystem
using Kturker.UnityBase.Core.Save;
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common.Save;
// /cmpose:showcase
// /cmpose:saveSystem
// cmpose:uiTheme
using Kturker.UnityBase.Core.Theme;
// /cmpose:uiTheme
// cmpose:multiLang
using Kturker.UnityBase.Core.Language;
// /cmpose:multiLang
// cmpose:network
using Kturker.UnityBase.Core.Network;
// cmpose:sample
using Kturker.UnityBase.Features.Sample.Network;
// /cmpose:sample
// /cmpose:network
// cmpose:networkInspector
using Kturker.UnityBase.Core.NetworkInspector;
// /cmpose:networkInspector
// cmpose:purchases
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Shell.Purchases;
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common.Purchases;
// /cmpose:showcase
// /cmpose:purchases
// cmpose:ads
using Kturker.UnityBase.Core.Ads;
// cmpose:sample
using Kturker.UnityBase.Features.Sample.Ads;
// /cmpose:sample
using Kturker.UnityBase.Features.Shell.Ads;
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common.Ads;
// /cmpose:showcase
// /cmpose:ads
// cmpose:leaderboards
using Kturker.UnityBase.Core.Leaderboards;
// cmpose:sample
using Kturker.UnityBase.Features.Sample.Leaderboards;
// /cmpose:sample
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common.Leaderboards;
// /cmpose:showcase
// /cmpose:leaderboards
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common;
// /cmpose:showcase
// cmpose:runner2D
using Kturker.UnityBase.App.Runner2D;
using Kturker.UnityBase.Features.Showcase.Runner2D;
// /cmpose:runner2D
// cmpose:track3D
using Kturker.UnityBase.App.Track3D;
using Kturker.UnityBase.Features.Showcase.Track3D;
// /cmpose:track3D

namespace Kturker.UnityBase.Tests.EditMode
{
    // Builds every scope from its installer, the way the scenes do, and resolves what each one registers.
    // A missing registration fails here instead of on a device.
    public sealed class DependencyGraphTests
    {
        private static IObjectResolver BuildRoot()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILoadingOverlay>(new FakeOverlay());
            builder.RegisterInstance<IAudioPlayer>(new FakeAudioPlayer());
            builder.RegisterInstance(new UiSounds(null));
            builder.RegisterInstance(ScriptableObject.CreateInstance<AppConfig>());
            new RootInstaller().Install(builder);
            // Last registration wins: keep EditMode away from the real scene manager and PlayerPrefs
            builder.Register<ISceneGateway, FakeGateway>(Lifetime.Singleton);
            builder.Register<IPreferences, FakePreferences>(Lifetime.Singleton);
            return builder.Build();
        }

        [Test]
        public void Root_scope_resolves_its_services_as_singletons()
        {
            using var root = BuildRoot();
            Assert.That(root.Resolve<ILog>(), Is.Not.Null);
            Assert.That(root.Resolve<AppInfo>(), Is.Not.Null);
            Assert.That(root.Resolve<BuildScenes>().Contains("Menu"), Is.True);
            Assert.That(root.Resolve<ISceneLoader>(), Is.SameAs(root.Resolve<ISceneLoader>()));
            Assert.That(root.Resolve<IPreferences>(), Is.InstanceOf<FakePreferences>());
            Assert.That(root.Resolve<AudioPreferences>(), Is.SameAs(root.Resolve<AudioPreferences>()));
            Assert.That(root.Resolve<HapticsService>(), Is.SameAs(root.Resolve<HapticsService>()));
            Assert.That(root.Resolve<IUiFeedback>(), Is.Not.Null);
            Assert.That(root.Resolve<ILocalizer>(), Is.SameAs(root.Resolve<TableLocalizer>()));
            Assert.That(root.Resolve<ILocalizer>().Get("menu.play"), Is.EqualTo("Play"));
            Assert.That(root.Resolve<UiBinder>(), Is.SameAs(root.Resolve<UiBinder>()));
            Assert.That(root.Resolve<IThemeSource>().Current, Is.Not.Null);
            Assert.That(root.Resolve<IThemeSource>().Style, Is.Not.Null);
            Assert.That(root.Resolve<PauseService>(), Is.SameAs(root.Resolve<PauseService>()));
            Assert.That(root.Resolve<DeepLinkRouter>(), Is.SameAs(root.Resolve<DeepLinkRouter>()));
            Assert.That(root.Resolve<DeepLinkQueue>(), Is.SameAs(root.Resolve<DeepLinkQueue>()));
            Assert.That(root.Resolve<AboutInfo>().VersionLabel, Does.StartWith("v"));
            Assert.That(root.Resolve<GameCredits>(), Is.SameAs(root.Resolve<GameCredits>()));
            Assert.That(root.Resolve<IUrlOpener>(), Is.Not.Null);
            Assert.That(root.Resolve<ReviewPrompter>(), Is.SameAs(root.Resolve<ReviewPrompter>()));
            Assert.That(root.Resolve<ReviewPrompter>().CanOpenStorePage, Is.True);
            Assert.That(root.Resolve<UpdateChecker>(), Is.SameAs(root.Resolve<UpdateChecker>()));
            Assert.That(root.Resolve<IHttpText>(), Is.InstanceOf<UnityWebRequestHttpText>());
            Assert.That(root.Resolve<IPlatformUpdater>(), Is.InstanceOf<NullPlatformUpdater>());
            var startables = root.Resolve<IReadOnlyList<IStartable>>();
            Assert.That(startables.OfType<PauseEffects>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<UnityDeepLinkSource>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<UpdateFlow>().Count(), Is.EqualTo(1));
            Assert.That(root.Resolve<UpdateFlow>(), Is.SameAs(startables.OfType<UpdateFlow>().Single()));
            var games = root.Resolve<IReadOnlyList<GameScene>>().Select(g => g.Scene).ToList();
            Assert.That(root.Resolve<GameScene>().Scene, Is.EqualTo(games.Last()));
            // cmpose:sample
            Assert.That(games, Does.Contain(SceneId.Game));
            // /cmpose:sample
            // cmpose:runner2D
            Assert.That(games, Does.Contain(SceneId.Runner2D));
            // /cmpose:runner2D
            // cmpose:track3D
            Assert.That(games, Does.Contain(SceneId.Track3D));
            Assert.That(root.Resolve<GameScene>().Scene, Is.EqualTo(SceneId.Track3D));
            // /cmpose:track3D
            Assert.That(root.Resolve<ILevelProgress>(), Is.SameAs(root.Resolve<ILevelProgress>()));
            Assert.That(root.Resolve<IShellRunEvents>(), Is.SameAs(root.Resolve<ShellRunEvents>()));
            Assert.That(root.Resolve<IEconomy>(), Is.SameAs(root.Resolve<Economy>()));
            Assert.That(root.Resolve<Supply>(), Is.SameAs(root.Resolve<Supply>()));
            Assert.That(root.Resolve<Wheel>(), Is.SameAs(root.Resolve<Wheel>()));
            Assert.That(root.Resolve<LoginBonus>(), Is.SameAs(root.Resolve<LoginBonus>()));
            var initializables = root.Resolve<IReadOnlyList<IInitializable>>();
            Assert.That(initializables.OfType<WheelRunCounter>().Count(), Is.EqualTo(1));
            Assert.That(root.Resolve<IShopCatalog>(), Is.SameAs(root.Resolve<EconomyShopCatalog>()));
            var windows = root.Resolve<EconomyWindows>();
            Assert.That(root.Resolve<IEconomyWindows>(), Is.SameAs(windows));
            Assert.That(root.Resolve<IBoosterOffer>(), Is.SameAs(windows));
            Assert.That(root.Resolve<IBoosterPicker>(), Is.SameAs(windows));
            Assert.That(root.Resolve<IReadOnlyList<ITickable>>(), Has.Some.SameAs(windows));
            // cmpose:saveSystem
            Assert.That(root.Resolve<ISaveStore>(), Is.InstanceOf<FileSaveStore>().And.SameAs(root.Resolve<ISaveStore>()));
            Assert.That(root.Resolve<ILevelProgress>(), Is.InstanceOf<LevelProgress>());
            Assert.That(root.Resolve<IEconomyStore>(), Is.InstanceOf<SavedEconomyStore>());
            // /cmpose:saveSystem
            // cmpose:uiTheme
            Assert.That(root.Resolve<IThemeSource>(), Is.SameAs(root.Resolve<SwitchableThemeSource>()));
            // /cmpose:uiTheme
            // cmpose:multiLang
            Assert.That(root.Resolve<LanguageSelector>(), Is.SameAs(root.Resolve<LanguageSelector>()));
            // /cmpose:multiLang
            // cmpose:network
            Assert.That(root.Resolve<IApiClient>(), Is.InstanceOf<ApiClient>().And.SameAs(root.Resolve<IApiClient>()));
            Assert.That(root.Resolve<IRemoteImageLoader>(), Is.InstanceOf<RemoteImageLoader>().And.SameAs(root.Resolve<IRemoteImageLoader>()));
            // /cmpose:network
            // cmpose:networkInspector
            Assert.That(root.Resolve<IReadOnlyList<IRequestObserver>>(), Has.Some.SameAs(root.Resolve<InspectorObserver>()));
            Assert.That(startables.OfType<InspectorOverlayHost>().Count(), Is.EqualTo(1));
            // /cmpose:networkInspector
            // cmpose:purchases
            Assert.That(root.Resolve<IPurchases>(), Is.InstanceOf<RevenueCatPurchases>().And.SameAs(startables.OfType<RevenueCatPurchases>().Single()));
            // The editor never configures RevenueCat (its no-op wrapper never answers)
            Assert.That(root.Resolve<IPurchases>().IsAvailable, Is.False);
            Assert.That(root.Resolve<IRevenueCatGateway>(), Is.InstanceOf<RevenueCatGateway>());
            Assert.That(root.Resolve<IShop>(), Is.InstanceOf<ShopHost>().And.SameAs(root.Resolve<IShop>()));
            Assert.That(root.Resolve<IEconomyShop>(), Is.InstanceOf<PurchasesEconomyShop>());
            Assert.That(root.Resolve<IPremiumStatus>(), Is.SameAs(root.Resolve<IPurchases>()));
            Assert.That(initializables.OfType<ShopGrants>().Single(), Is.SameAs(root.Resolve<ShopGrants>()));
            // /cmpose:purchases
            // cmpose:ads
            Assert.That(root.Resolve<IAds>(), Is.InstanceOf<GmaAds>().And.SameAs(root.Resolve<GmaAds>()));
            // The editor never starts Google Mobile Ads
            Assert.That(root.Resolve<IAds>().IsAvailable, Is.False);
            Assert.That(root.Resolve<IGmaGateway>(), Is.InstanceOf<GmaGateway>());
            Assert.That(root.Resolve<ITrackingAuthorization>(), Is.InstanceOf<NoTrackingAuthorization>());
            Assert.That(root.Resolve<IAdsPrivacy>(), Is.SameAs(startables.OfType<AdsConsentFlow>().Single()));
            Assert.That(startables.OfType<MenuBanner>().Count(), Is.EqualTo(1));
            Assert.That(root.Resolve<IEconomyAds>(), Is.InstanceOf<RewardedEconomyAds>());
            // /cmpose:ads
            // cmpose:leaderboards
            Assert.That(root.Resolve<IGameServices>(), Is.InstanceOf<GameServices>().And.SameAs(startables.OfType<GameServices>().Single()));
            // The editor never touches Google Play Games or Game Center
            Assert.That(root.Resolve<IGameServices>().IsAvailable, Is.False);
            Assert.That(root.Resolve<IGameServicesGateway>(), Is.InstanceOf<NoGameServicesGateway>());
            // /cmpose:leaderboards
        }

        [Test]
        public void Boot_scope_resolves_the_boot_entry()
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope(builder =>
            {
                builder.RegisterInstance<IBootErrorView>(new FakeBootErrorView());
                new BootInstaller(registerViews: false).Install(builder);
            });
            Assert.That(scope.Resolve<IReadOnlyList<IAsyncStartable>>().OfType<BootEntry>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<IReadOnlyList<IStartable>>().OfType<SceneUiBinding>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Menu_scope_resolves_the_menu_and_its_panels()
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope(builder =>
            {
                builder.RegisterInstance<IMenuView>(new FakeMenuView());
                builder.RegisterInstance<ISettingsView>(new FakeSettingsView());
                builder.RegisterInstance<IAboutView>(new FakeAboutView());
                builder.RegisterInstance<IUpdateView>(new FakeUpdateView());
                var sides = new FakeMenuSideButtons();
                builder.RegisterInstance<IMenuSideButtons>(sides);
                builder.RegisterInstance<IMenuCoins>(sides);
                // cmpose:showcase
                builder.RegisterInstance<IShowcaseMenuView>(new FakeShowcaseMenuView());
                // cmpose:purchases
                builder.RegisterInstance<IShopButtonView>(new FakeShopButtonView());
                // /cmpose:purchases
                // cmpose:leaderboards
                builder.RegisterInstance<IShowcaseScoresView>(new FakeShowcaseScoresView());
                // /cmpose:leaderboards
                // cmpose:saveSystem
                builder.RegisterInstance<IRunStatsView>(new FakeRunStatsView());
                // /cmpose:saveSystem
                // /cmpose:showcase
                new MenuInstaller(registerViews: false).Install(builder);
            });
            var startables = scope.Resolve<IReadOnlyList<IStartable>>();
            Assert.That(startables.OfType<SceneUiBinding>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<MenuPresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<UpdatePresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<EconomyMenu>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<ISettingsPanel>(), Is.SameAs(startables.OfType<SettingsPresenter>().Single()));
            Assert.That(scope.Resolve<IAboutPanel>(), Is.SameAs(startables.OfType<AboutPresenter>().Single()));
            // cmpose:showcase
            Assert.That(startables.OfType<ShowcaseMenuPresenter>().Count(), Is.EqualTo(1));
            // cmpose:purchases
            Assert.That(startables.OfType<ShowcaseShopPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:purchases
            // cmpose:leaderboards
            Assert.That(startables.OfType<ShowcaseScoresPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:leaderboards
            // cmpose:saveSystem
            Assert.That(startables.OfType<RunStatsPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:saveSystem
            // /cmpose:showcase
            // cmpose:uiTheme
            Assert.That(scope.Resolve<IReadOnlyList<ISettingsSection>>().OfType<ThemeSection>().Count(), Is.EqualTo(1));
            // /cmpose:uiTheme
            // cmpose:multiLang
            Assert.That(scope.Resolve<IReadOnlyList<ISettingsSection>>().OfType<LanguageSelector>().Count(), Is.EqualTo(1));
            // /cmpose:multiLang
            // cmpose:purchases
            // The Remove Ads row only in a game with ads
            var removeAdsRows = 0;
            // cmpose:ads
            removeAdsRows = 1;
            // /cmpose:ads
            Assert.That(scope.Resolve<IReadOnlyList<ISettingsSection>>().OfType<PremiumSection>().Count(), Is.EqualTo(removeAdsRows));
            // /cmpose:purchases
            // cmpose:ads
            Assert.That(scope.Resolve<IReadOnlyList<ISettingsSection>>().OfType<PrivacySection>().Count(), Is.EqualTo(1));
            // /cmpose:ads
            // cmpose:leaderboards
            Assert.That(scope.Resolve<IReadOnlyList<ISettingsSection>>().OfType<GameServicesSection>().Count(), Is.EqualTo(1));
            // /cmpose:leaderboards
        }

        // cmpose:sample
        [Test]
        public void Game_scope_resolves_pause_guide_settings_and_feature_presenters()
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope(builder =>
            {
                builder.RegisterInstance<ISampleView>(new FakeSampleView());
                builder.RegisterInstance<ISettingsView>(new FakeSettingsView());
                builder.RegisterInstance<IPauseView>(new FakePauseView());
                builder.RegisterInstance<IGuideView>(new FakeGuideView());
                builder.RegisterInstance<IShellHudView>(new FakeShellHudView());
                builder.RegisterInstance<IStartHintView>(new FakeStartHintView());
                builder.RegisterInstance<IGameOverCard>(new FakeGameOverCard());
                builder.RegisterInstance<ILevelCompleteCard>(new FakeLevelCompleteCard());
                builder.RegisterInstance<ILevelFailedCard>(new FakeLevelFailedCard());
                builder.RegisterInstance<IBoosterBarView>(new FakeBoosterBarView());
                // cmpose:network
                builder.RegisterInstance<IGitHubUserCardView>(new FakeGitHubUserCardView());
                // /cmpose:network
                // cmpose:ads
                builder.RegisterInstance<IBonusView>(new FakeBonusView());
                // /cmpose:ads
                // cmpose:leaderboards
                builder.RegisterInstance<ILeaderboardView>(new FakeLeaderboardView());
                // /cmpose:leaderboards
                new GameInstaller(registerViews: false).Install(builder);
            });
            var startables = scope.Resolve<IReadOnlyList<IStartable>>();
            Assert.That(startables.OfType<SceneUiBinding>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GameRenderMarker>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<IReadOnlyList<ITickable>>().OfType<FrameTimeMarker>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<SamplePresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<PausePresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GuidePresenter>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<ISettingsPanel>(), Is.SameAs(startables.OfType<SettingsPresenter>().Single()));
            Assert.That(scope.Resolve<IGameShell>(), Is.InstanceOf<GameShell>().And.SameAs(scope.Resolve<GameShell>()));
            Assert.That(scope.Resolve<IShellAds>(), Is.Not.Null);
            Assert.That(scope.Resolve<IBoosters>(), Is.InstanceOf<Boosters>().And.SameAs(scope.Resolve<Boosters>()));
            Assert.That(startables.OfType<BoosterBar>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<EconomyHud>().Count(), Is.EqualTo(1));
            // cmpose:ads
            Assert.That(scope.Resolve<IShellAds>(), Is.InstanceOf<RewardedShellAds>());
            // /cmpose:ads
            var counter = scope.Resolve<ICompletionCounter>();
            Assert.That(counter, Is.Not.Null);
            // cmpose:saveSystem
            Assert.That(counter, Is.InstanceOf<SavedCompletionCounter>());
            // /cmpose:saveSystem
            // cmpose:network
            Assert.That(startables.OfType<GitHubUserPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:network
            // cmpose:ads
            Assert.That(startables.OfType<SampleAdsPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:ads
            // cmpose:leaderboards
            Assert.That(startables.OfType<SampleLeaderboardsPresenter>().Count(), Is.EqualTo(1));
            // /cmpose:leaderboards
        }
        // /cmpose:sample

        // cmpose:runner2D
        [Test]
        public void Runner2D_scope_resolves_the_runner_pause_settings_and_shared_parts()
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope(builder =>
            {
                builder.RegisterInstance<ISettingsView>(new FakeSettingsView());
                builder.RegisterInstance<IPauseView>(new FakePauseView());
                builder.RegisterInstance<IRunnerWorldView>(new FakeRunnerWorldView());
                builder.RegisterInstance<IShowcaseHudView>(new FakeShowcaseHudView());
                builder.RegisterInstance<ITapInput>(new FakeTapInput());
                builder.RegisterInstance<IGameOverView>(new FakeGameOverView());
                // cmpose:ads
                builder.RegisterInstance<IContinueView>(new FakeContinueView());
                // /cmpose:ads
                new Runner2DInstaller(registerViews: false).Install(builder);
            });
            var startables = scope.Resolve<IReadOnlyList<IStartable>>();
            Assert.That(startables.OfType<SceneUiBinding>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GameRenderMarker>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<IReadOnlyList<ITickable>>().OfType<FrameTimeMarker>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<PausePresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GameOverPresenter>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<ISettingsPanel>(), Is.SameAs(startables.OfType<SettingsPresenter>().Single()));
            var runner = startables.OfType<Runner2DPresenter>().Single();
            Assert.That(scope.Resolve<IShowcaseRun>(), Is.SameAs(runner));
            Assert.That(scope.Resolve<IReadOnlyList<ITickable>>(), Has.Some.SameAs(runner));
            // cmpose:ads
            Assert.That(startables.OfType<ShowcaseContinuePresenter>().Count(), Is.EqualTo(1));
            // /cmpose:ads
            // cmpose:leaderboards
            Assert.That(startables.OfType<ShowcaseScoreReporter>().Count(), Is.EqualTo(1));
            // /cmpose:leaderboards
            // cmpose:saveSystem
            Assert.That(startables.OfType<RunStatsRecorder>().Count(), Is.EqualTo(1));
            // /cmpose:saveSystem
        }
        // /cmpose:runner2D

        // cmpose:track3D
        [Test]
        public void Track3D_scope_resolves_the_track_pause_settings_and_shared_parts()
        {
            using var root = BuildRoot();
            using var scope = root.CreateScope(builder =>
            {
                builder.RegisterInstance<ISettingsView>(new FakeSettingsView());
                builder.RegisterInstance<IPauseView>(new FakePauseView());
                builder.RegisterInstance<ITrackWorldView>(new FakeTrackWorldView());
                builder.RegisterInstance<IShowcaseHudView>(new FakeShowcaseHudView());
                builder.RegisterInstance<ITrackInput>(new FakeTrackInput());
                builder.RegisterInstance<IGameOverView>(new FakeGameOverView());
                // cmpose:ads
                builder.RegisterInstance<IContinueView>(new FakeContinueView());
                // /cmpose:ads
                new Track3DInstaller(registerViews: false).Install(builder);
            });
            var startables = scope.Resolve<IReadOnlyList<IStartable>>();
            Assert.That(startables.OfType<SceneUiBinding>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GameRenderMarker>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<IReadOnlyList<ITickable>>().OfType<FrameTimeMarker>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<PausePresenter>().Count(), Is.EqualTo(1));
            Assert.That(startables.OfType<GameOverPresenter>().Count(), Is.EqualTo(1));
            Assert.That(scope.Resolve<ISettingsPanel>(), Is.SameAs(startables.OfType<SettingsPresenter>().Single()));
            var track = startables.OfType<Track3DPresenter>().Single();
            Assert.That(scope.Resolve<IShowcaseRun>(), Is.SameAs(track));
            Assert.That(scope.Resolve<IReadOnlyList<ITickable>>(), Has.Some.SameAs(track));
            // cmpose:ads
            Assert.That(startables.OfType<ShowcaseContinuePresenter>().Count(), Is.EqualTo(1));
            // /cmpose:ads
            // cmpose:leaderboards
            Assert.That(startables.OfType<ShowcaseScoreReporter>().Count(), Is.EqualTo(1));
            // /cmpose:leaderboards
            // cmpose:saveSystem
            Assert.That(startables.OfType<RunStatsRecorder>().Count(), Is.EqualTo(1));
            // /cmpose:saveSystem
        }
        // /cmpose:track3D
    }
}
