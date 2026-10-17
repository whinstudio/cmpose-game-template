using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.DeepLink;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Logging;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;
using Kturker.UnityBase.Core.Update;
using Kturker.UnityBase.Features.Shell.GameShell;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// cmpose:saveSystem
using Kturker.UnityBase.Core.Save;
// /cmpose:saveSystem
// cmpose:uiTheme
using Kturker.UnityBase.Core.Theme;
// /cmpose:uiTheme
// cmpose:multiLang
using Kturker.UnityBase.Core.Language;
// /cmpose:multiLang
// cmpose:network
using Kturker.UnityBase.Core.Network;
// /cmpose:network
// cmpose:networkInspector
using Kturker.UnityBase.Core.NetworkInspector;
// /cmpose:networkInspector
// cmpose:purchases
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Shell.Purchases;
// /cmpose:purchases
// cmpose:ads
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Features.Shell.Ads;
// /cmpose:ads
// cmpose:leaderboards
using Kturker.UnityBase.Core.Leaderboards;
// /cmpose:leaderboards

namespace Kturker.UnityBase.Core.DI
{
    // Services that live for the whole app; components and assets on the root prefab are registered by ProjectLifetimeScope
    public sealed class RootInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ILog>(_ => new UnityLog(Debug.isDebugBuild), Lifetime.Singleton);
            builder.Register<IStringSource, ResourcesStringSource>(Lifetime.Singleton);
            builder.Register<TableLocalizer>(resolver =>
                new TableLocalizer(resolver.Resolve<IStringSource>(), resolver.Resolve<ILog>(), Debug.isDebugBuild), Lifetime.Singleton)
                .As<ILocalizer>().AsSelf();
            builder.Register<IThemeSource>(_ => new FixedThemeSource(Palettes.Load(Palettes.Light), ThemeStyle.Load()), Lifetime.Singleton);
            builder.Register<UiBinder>(Lifetime.Singleton);
            builder.RegisterInstance(new AppInfo(Application.productName, Application.version, Application.platform.ToString()));
            builder.RegisterInstance(BuildScenes.FromBuildSettings());
            // The games the menu offers (each game's flag registers its own). Transient: VContainer refuses two singleton
            // registrations of one type in a collection, and a GameScene is an immutable value.
            // cmpose:sample
            builder.Register(_ => new GameScene(SceneId.Game, "menu.play"), Lifetime.Transient);
            // /cmpose:sample
            // cmpose:runner2D
            builder.Register(_ => new GameScene(SceneId.Runner2D, "runner2d.play"), Lifetime.Transient);
            // /cmpose:runner2D
            // cmpose:track3D
            builder.Register(_ => new GameScene(SceneId.Track3D, "track3d.play"), Lifetime.Transient);
            // /cmpose:track3D
            builder.Register<ISceneGateway, UnitySceneGateway>(Lifetime.Singleton);
            builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
            builder.Register<IPreferences, PlayerPrefsPreferences>(Lifetime.Singleton);
            builder.Register<AudioPreferences>(Lifetime.Singleton);
            builder.Register<IVibrator>(resolver => Vibrators.ForPlatform(resolver.Resolve<ILog>()), Lifetime.Singleton);
            builder.Register<HapticsService>(Lifetime.Singleton);
            builder.Register<IUiFeedback, UiFeedback>(Lifetime.Singleton);
            builder.Register<PauseService>(Lifetime.Singleton);
            builder.Register<ITimeScale, UnityTimeScale>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PauseEffects>();
            builder.Register<DeepLinkRouter>(Lifetime.Singleton);
            builder.Register<DeepLinkQueue>(Lifetime.Singleton);
            builder.RegisterEntryPoint<UnityDeepLinkSource>();
            builder.Register<AboutInfo>(resolver => AboutInfo.From(resolver.Resolve<AppInfo>(), resolver.Resolve<AppConfig>()), Lifetime.Singleton);
            builder.Register<IUrlOpener, UnityUrlOpener>(Lifetime.Singleton);
            builder.Register<StoreIdentity>(resolver =>
                new StoreIdentity(CurrentStore(), Application.identifier, resolver.Resolve<AppConfig>().AppStoreId), Lifetime.Singleton);
            builder.Register<IReviewRequester>(resolver => ReviewRequesters.ForPlatform(resolver.Resolve<ILog>()), Lifetime.Singleton);
            builder.Register<ReviewPrompter>(Lifetime.Singleton);
            builder.Register<IHttpText, UnityWebRequestHttpText>(Lifetime.Singleton);
            builder.Register<IClock, SystemClock>(Lifetime.Singleton);
            builder.Register<IPlatformUpdater>(resolver => PlatformUpdaters.ForPlatform(resolver.Resolve<ILog>()), Lifetime.Singleton);
            builder.Register<UpdateConfig>(resolver =>
                new UpdateConfig(resolver.Resolve<AppConfig>().MinVersionUrl, Application.identifier), Lifetime.Singleton);
            builder.Register<UpdateChecker>(Lifetime.Singleton);
            builder.RegisterEntryPoint<UpdateFlow>().AsSelf();
            // The game shell's level flow (the menu reads it too); a save store keeps it
            builder.Register<ILevelProgress, NoLevelProgress>(Lifetime.Singleton);
            // cmpose:saveSystem
            new SaveInstaller().Install(builder);
            builder.Register<ILevelProgress, LevelProgress>(Lifetime.Singleton);
            // /cmpose:saveSystem
            // cmpose:uiTheme
            new ThemeInstaller().Install(builder);
            // /cmpose:uiTheme
            // cmpose:multiLang
            new LanguageInstaller().Install(builder);
            // /cmpose:multiLang
            // cmpose:network
            new NetworkInstaller().Install(builder);
            // /cmpose:network
            // cmpose:networkInspector
            new InspectorInstaller().Install(builder);
            // /cmpose:networkInspector
            // cmpose:purchases
            new PurchasesInstaller().Install(builder);
            new PaywallInstaller().Install(builder);
            // /cmpose:purchases
            // cmpose:ads
            new AdsInstaller().Install(builder);
            new ShellAdsInstaller().Install(builder);
            // /cmpose:ads
            // cmpose:leaderboards
            new LeaderboardsInstaller().Install(builder);
            // /cmpose:leaderboards
        }

        // The editor counts as Android
        private static StorePlatform CurrentStore() =>
            Application.platform == RuntimePlatform.IPhonePlayer ? StorePlatform.Ios : StorePlatform.Android;
    }
}
