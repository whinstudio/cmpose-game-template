using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Theme
{
    // uiTheme flag. Registered after the core's FixedThemeSource, so IThemeSource resolves to this one.
    public sealed class ThemeInstaller : IInstaller
    {
        public const string DarkPalette = "Theme/Dark";

        public void Install(IContainerBuilder builder)
        {
            builder.Register<ISystemDarkMode>(resolver => SystemDarkModes.ForPlatform(resolver.Resolve<ILog>()), Lifetime.Singleton);
            builder.Register<SwitchableThemeSource>(resolver => new SwitchableThemeSource(resolver.Resolve<IPreferences>(),
                    resolver.Resolve<ISystemDarkMode>(), resolver.Resolve<PauseService>(), resolver.Resolve<ILog>(),
                    Palettes.Load(Palettes.Light), Palettes.Load(DarkPalette), ThemeStyle.Load()), Lifetime.Singleton)
                .As<IThemeSource>().AsSelf();
            builder.Register<ISettingsSection, ThemeSection>(Lifetime.Singleton);
            builder.RegisterBuildCallback(resolver => resolver.Resolve<SwitchableThemeSource>().Start());
        }
    }
}
