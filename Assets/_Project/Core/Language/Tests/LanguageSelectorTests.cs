using System.Linq;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Language.Tests
{
    public sealed class LanguageSelectorTests
    {
        private sealed class FakeSystemLanguage : ISystemLanguage
        {
            public SystemLanguage Language { get; set; } = SystemLanguage.English;
        }

        private FakePreferences prefs;
        private FakeSystemLanguage system;
        private FakeLog log;
        private TableLocalizer localizer;
        private LanguageSelector selector;

        [SetUp]
        public void Create()
        {
            prefs = new FakePreferences();
            system = new FakeSystemLanguage();
            log = new FakeLog();
            localizer = new TableLocalizer(new FakeStringSource()
                .Add("en", "core", "{\"menu.play\":\"Play\",\"boot.error.step\":\"Could not start: {0}\"}")
                .Add("tr", "core", "{\"menu.play\":\"Oyna\",\"boot.error.step\":\"Başlatılamadı: {0}\"}")
                .Add("fr", "core", "{\"menu.play\":\"Jouer\",\"boot.error.step\":\"Impossible de démarrer : {0}\"}")
                .Add("it", "core", "{\"menu.play\":\"Gioca\",\"boot.error.step\":\"Impossibile avviare: {0}\"}"), new FakeLog(), false);
            selector = new LanguageSelector(localizer, prefs, system, log);
        }

        [Test]
        public void The_system_language_decides_the_first_launch()
        {
            system.Language = SystemLanguage.Turkish;
            selector.Apply();

            Assert.That(localizer.Language, Is.EqualTo("tr"));
            Assert.That(selector.Current, Is.EqualTo("tr"));
            Assert.That(selector.FollowsDevice, Is.True);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_LANG code=tr"));
        }

        [Test]
        public void A_stored_choice_wins_over_the_system_language()
        {
            prefs.SetString(LanguageSelector.PreferenceKey, "fr");
            system.Language = SystemLanguage.Turkish;
            selector.Apply();

            Assert.That(localizer.Language, Is.EqualTo("fr"));
        }

        [Test]
        public void An_unknown_stored_or_system_language_falls_back()
        {
            prefs.SetString(LanguageSelector.PreferenceKey, "xx");
            system.Language = SystemLanguage.German;
            selector.Apply();
            Assert.That(localizer.Language, Is.EqualTo("en"));

            prefs.SetString(LanguageSelector.PreferenceKey, "");
            system.Language = SystemLanguage.Spanish;
            selector.Apply();
            Assert.That(localizer.Language, Is.EqualTo("en"));
            Assert.That(log.Lines.Last(), Is.EqualTo("MARKER CMPOSE_LANG code=en"));
        }

        [Test]
        public void Selecting_stores_switches_marks_and_raises_Changed()
        {
            selector.Apply();
            var changes = 0;
            selector.Changed += () => changes++;

            selector.Select(1);
            Assert.That(prefs.Values[LanguageSelector.PreferenceKey], Is.EqualTo("tr"));
            Assert.That(localizer.Language, Is.EqualTo("tr"));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(log.Lines.Last(), Is.EqualTo("MARKER CMPOSE_LANG code=tr"));

            selector.Select(9);
            selector.Select(-1);
            Assert.That(localizer.Language, Is.EqualTo("tr"));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Languages_are_the_supported_ones_by_their_own_names()
        {
            Assert.That(selector.Languages, Is.SameAs(LanguageSelector.Supported));
            Assert.That(selector.Languages.Select(l => l.Code), Is.EqualTo(new[] { "en", "tr", "fr", "es", "it" }));
            Assert.That(selector.Languages.Select(l => l.Name), Is.EqualTo(new[] { "English", "Türkçe", "Français", "Español", "Italiano" }));
        }

        [Test]
        public void Following_the_device_reads_the_device_language_on_every_apply()
        {
            prefs.SetString(LanguageSelector.PreferenceKey, "tr");
            selector.Apply();
            var changes = 0;
            selector.Changed += () => changes++;

            system.Language = SystemLanguage.French;
            selector.FollowDevice();
            Assert.That(prefs.Values.ContainsKey(LanguageSelector.PreferenceKey), Is.False, "lang.code deleted");
            Assert.That(selector.FollowsDevice, Is.True);
            Assert.That(selector.Current, Is.EqualTo("fr"));
            Assert.That(changes, Is.EqualTo(1));

            selector.Apply();
            Assert.That(selector.Current, Is.EqualTo("fr"));

            // The player changes the device language and starts the game again
            system.Language = SystemLanguage.Italian;
            var relaunched = new LanguageSelector(localizer, prefs, system, log);
            relaunched.Apply();
            Assert.That(relaunched.Current, Is.EqualTo("it"));
            Assert.That(log.Lines.Last(), Is.EqualTo("MARKER CMPOSE_LANG code=it"));
        }

        [Test]
        public void An_unsupported_device_language_falls_back_to_english()
        {
            system.Language = SystemLanguage.Japanese;
            selector.FollowDevice();
            Assert.That(selector.Current, Is.EqualTo("en"));

            system.Language = SystemLanguage.German;
            new LanguageSelector(localizer, prefs, system, log).Apply();
            Assert.That(localizer.Language, Is.EqualTo("en"));
        }

        [Test]
        public void Device_is_the_device_language_even_after_a_choice()
        {
            system.Language = SystemLanguage.French;
            selector.Choose("tr");
            Assert.That(selector.Device, Is.EqualTo("fr"));

            system.Language = SystemLanguage.Japanese;
            Assert.That(selector.Device, Is.EqualTo("en"), "unsupported: English");
        }

        [Test]
        public void Choose_stores_the_code_and_wins_over_the_device()
        {
            system.Language = SystemLanguage.Turkish;
            selector.Apply();
            var changes = 0;
            selector.Changed += () => changes++;

            selector.Choose("fr");
            Assert.That(prefs.Values[LanguageSelector.PreferenceKey], Is.EqualTo("fr"));
            Assert.That(selector.Current, Is.EqualTo("fr"));
            Assert.That(selector.FollowsDevice, Is.False);
            Assert.That(changes, Is.EqualTo(1));

            var relaunched = new LanguageSelector(localizer, prefs, system, log);
            relaunched.Apply();
            Assert.That(relaunched.Current, Is.EqualTo("fr"), "the stored choice wins over the Turkish device");

            selector.Choose("xx");
            selector.Choose(null);
            Assert.That(prefs.Values[LanguageSelector.PreferenceKey], Is.EqualTo("fr"), "an unsupported code does nothing");
            Assert.That(changes, Is.EqualTo(1));
        }

        // Compass's UI shot test (unity-kit) picks languages by reflection through Supported and Select(int)
        [Test]
        public void Select_by_index_still_works_for_the_kit_shot_test()
        {
            var select = typeof(LanguageSelector).GetMethod("Select", new[] { typeof(int) });
            Assert.That(select, Is.Not.Null);
            Assert.That(typeof(LanguageSelector).GetField("Supported"), Is.Not.Null);

            selector.Select(1);

            Assert.That(selector.Current, Is.EqualTo("tr"));
            Assert.That(prefs.Values[LanguageSelector.PreferenceKey], Is.EqualTo("tr"));
        }

        [Test]
        public void Switching_rewrites_keyed_text_and_keeps_literals()
        {
            var keyedGo = new GameObject("Keyed", typeof(RectTransform));
            var literalGo = new GameObject("Literal", typeof(RectTransform));
            try
            {
                var keyedLabel = keyedGo.AddComponent<TextMeshProUGUI>();
                var keyed = keyedGo.AddComponent<LocalizedText>();
                var literalLabel = literalGo.AddComponent<TextMeshProUGUI>();
                var literal = literalGo.AddComponent<LocalizedText>();
                keyed.SetKey("boot.error.step", "audio");
                literal.SetLiteral("v1.0.0");
                keyed.Bind(localizer);
                literal.Bind(localizer);
                selector.Apply();
                Assert.That(keyedLabel.text, Is.EqualTo("Could not start: audio"));

                selector.Select(1);
                Assert.That(keyedLabel.text, Is.EqualTo("Başlatılamadı: audio"));
                Assert.That(literalLabel.text, Is.EqualTo("v1.0.0"));
            }
            finally
            {
                Object.DestroyImmediate(keyedGo);
                Object.DestroyImmediate(literalGo);
            }
        }
    }
}
