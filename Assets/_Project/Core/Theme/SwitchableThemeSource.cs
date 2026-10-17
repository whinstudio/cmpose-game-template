using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Theme
{
    // uiTheme flag: Light, Dark or System (default), stored in theme.mode. System follows the device and is read
    // again whenever the app comes back (PauseService.Resumed). Changed fires when the palette in use changes.
    public sealed class SwitchableThemeSource : IThemeSource, IDisposable
    {
        public const string PreferenceKey = "theme.mode";

        private readonly IPreferences preferences;
        private readonly ISystemDarkMode system;
        private readonly PauseService pause;
        private readonly ILog log;
        private readonly Palette light;
        private readonly Palette dark;
        private readonly ThemeStyle style;
        private bool started;
        private bool isDark;

        public SwitchableThemeSource(IPreferences preferences, ISystemDarkMode system, PauseService pause, ILog log, Palette light, Palette dark, ThemeStyle style)
        {
            this.preferences = preferences;
            this.system = system;
            this.pause = pause;
            this.log = log;
            this.light = light;
            this.dark = dark;
            this.style = style;
        }

        public ThemeMode Mode { get; private set; }
        public bool IsDark => isDark;
        public Palette Current => isDark ? dark : light;
        public ThemeStyle Style => style;

        public event Action Changed;

        // Called once when the root scope is built; texts bound earlier repaint through Changed
        public void Start()
        {
            if (started) return;
            started = true;
            var stored = preferences.GetInt(PreferenceKey, (int)ThemeMode.System);
            Mode = Enum.IsDefined(typeof(ThemeMode), stored) ? (ThemeMode)stored : ThemeMode.System;
            isDark = ThemeModes.IsDark(Mode, system.IsDark);
            pause.Resumed += Refresh;
            Mark();
            Changed?.Invoke();
        }

        public void SetMode(ThemeMode mode)
        {
            Mode = mode;
            preferences.SetInt(PreferenceKey, (int)mode);
            isDark = ThemeModes.IsDark(Mode, system.IsDark);
            Mark();
            Changed?.Invoke();
        }

        public void Refresh()
        {
            var nowDark = ThemeModes.IsDark(Mode, system.IsDark);
            if (nowDark == isDark) return;
            isDark = nowDark;
            Mark();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (started) pause.Resumed -= Refresh;
        }

        private void Mark() => log.Marker($"CMPOSE_THEME mode={Mode} dark={(isDark ? "true" : "false")}");
    }
}
