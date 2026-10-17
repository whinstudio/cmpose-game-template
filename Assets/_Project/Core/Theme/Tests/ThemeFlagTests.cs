using System.Linq;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;
using UnityEngine;

namespace Kturker.UnityBase.Core.Theme.Tests
{
    public sealed class ThemeFlagTests
    {
        private sealed class FakeSystemDarkMode : ISystemDarkMode
        {
            public bool IsDark { get; set; }
        }

        private FakePreferences prefs;
        private FakeSystemDarkMode system;
        private PauseService pause;
        private FakeLog log;
        private Palette light;
        private Palette dark;
        private ThemeStyle style;
        private SwitchableThemeSource theme;

        [SetUp]
        public void Create()
        {
            prefs = new FakePreferences();
            system = new FakeSystemDarkMode();
            pause = new PauseService();
            log = new FakeLog();
            light = ScriptableObject.CreateInstance<Palette>();
            dark = ScriptableObject.CreateInstance<Palette>();
            style = ScriptableObject.CreateInstance<ThemeStyle>();
            theme = new SwitchableThemeSource(prefs, system, pause, log, light, dark, style);
        }

        [TearDown]
        public void Destroy()
        {
            theme.Dispose();
            Object.DestroyImmediate(light);
            Object.DestroyImmediate(dark);
            Object.DestroyImmediate(style);
        }

        private void ReturnToTheApp()
        {
            pause.Pause(PauseReason.Focus);
            pause.Resume(PauseReason.Focus);
        }

        [Test]
        public void The_dark_palette_sets_boxes_apart_and_primary_on_track()
        {
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<Palette>("Assets/_Project/Core/Theme/Resources/Theme/Dark.asset");
            Assert.That(palette, Is.Not.Null);
            Contrast.AssertBoxes(palette, "dark", accentOnRow: 2.3f, controlOnSurface: 3f);
        }

        [Test]
        public void The_dark_palette_has_readable_accent_success_and_danger()
        {
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<Palette>("Assets/_Project/Core/Theme/Resources/Theme/Dark.asset");
            Contrast.AssertAccentAndStatus(palette, "dark");
            Contrast.AssertAccentReads(palette, "dark");
            Contrast.AssertDangerReads(palette, "dark");
        }

        [Test]
        public void Light_and_dark_share_the_style()
        {
            theme.Start();
            Assert.That(theme.Style, Is.SameAs(style));
            theme.SetMode(ThemeMode.Dark);
            Assert.That(theme.Style, Is.SameAs(style));
        }

        [Test]
        public void Light_dark_and_system_resolve_against_the_device()
        {
            Assert.That(ThemeModes.IsDark(ThemeMode.Light, true), Is.False);
            Assert.That(ThemeModes.IsDark(ThemeMode.Dark, false), Is.True);
            Assert.That(ThemeModes.IsDark(ThemeMode.System, true), Is.True);
            Assert.That(ThemeModes.IsDark(ThemeMode.System, false), Is.False);
        }

        [Test]
        public void System_mode_follows_the_device_from_the_start()
        {
            system.IsDark = true;
            theme.Start();

            Assert.That(theme.Mode, Is.EqualTo(ThemeMode.System));
            Assert.That(theme.Current, Is.SameAs(dark));
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_THEME mode=System dark=true"));
        }

        [Test]
        public void Choosing_a_mode_stores_it_and_switches_the_palette()
        {
            theme.Start();
            var changes = 0;
            theme.Changed += () => changes++;

            theme.SetMode(ThemeMode.Dark);
            Assert.That(theme.Current, Is.SameAs(dark));
            Assert.That(prefs.Values[SwitchableThemeSource.PreferenceKey], Is.EqualTo((int)ThemeMode.Dark));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_THEME mode=Dark dark=true"));

            theme.SetMode(ThemeMode.Light);
            Assert.That(theme.Current, Is.SameAs(light));
        }

        [Test]
        public void Returning_to_the_app_rereads_the_device_and_changes_only_when_the_result_does()
        {
            theme.Start();
            var changes = 0;
            theme.Changed += () => changes++;

            system.IsDark = true;
            ReturnToTheApp();
            Assert.That(theme.Current, Is.SameAs(dark));
            Assert.That(changes, Is.EqualTo(1));
            ReturnToTheApp();
            Assert.That(changes, Is.EqualTo(1));

            theme.SetMode(ThemeMode.Light);
            system.IsDark = false;
            ReturnToTheApp();
            Assert.That(theme.Current, Is.SameAs(light));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void A_corrupt_stored_mode_reads_as_system()
        {
            foreach (var stored in new[] { 99, -1 })
            {
                prefs.SetInt(SwitchableThemeSource.PreferenceKey, stored);
                var source = new SwitchableThemeSource(prefs, system, pause, log, light, dark, style);
                source.Start();
                Assert.That(source.Mode, Is.EqualTo(ThemeMode.System), stored.ToString());
                source.Dispose();
            }
        }

        [Test]
        public void The_section_lists_system_light_dark_and_selects_through_the_source()
        {
            theme.Start();
            var section = new ThemeSection(theme);
            var changes = 0;
            section.Changed += () => changes++;

            Assert.That(section.TitleKey, Is.EqualTo("settings.theme"));
            Assert.That(section.Options.Select(o => o.LabelKey), Is.EqualTo(new[] { "theme.system", "theme.light", "theme.dark" }));
            Assert.That(section.Selected, Is.EqualTo(0));
            section.Select(2);
            Assert.That(theme.Mode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(section.Selected, Is.EqualTo(2));
            Assert.That(changes, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void An_invalid_index_is_ignored()
        {
            theme.Start();
            var section = new ThemeSection(theme);
            section.Select(3);
            section.Select(-1);
            Assert.That(theme.Mode, Is.EqualTo(ThemeMode.System));
        }
    }
}
