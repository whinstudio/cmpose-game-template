using System.Collections.Generic;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The settings card's view on a copy of its prefab (Awake does not run here): the switches show the values they
    // are given at once, a message takes the footer's place, and the language rows mark the chosen language
    public sealed class SettingsViewTests
    {
        private const string SettingsPrefab = "Assets/_Project/App/Settings/SettingsPanel.prefab";
        private const string Card = "Panel/SafeArea/Card";

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private SettingsView View()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefab);
            Assert.That(prefab, Is.Not.Null, SettingsPrefab);
            var copy = Object.Instantiate(prefab);
            made.Add(copy);
            return copy.GetComponent<SettingsView>();
        }

        private static Transform Find(SettingsView view, string path)
        {
            var found = view.transform.Find($"{Card}/{path}");
            Assert.That(found, Is.Not.Null, path);
            return found;
        }

        private static KitSwitch Switch(SettingsView view, string row) => Find(view, $"Body/Rows/{row}/Control").GetComponent<KitSwitch>();

        private static void AssertSwitch(SettingsView view, string row, bool on)
        {
            var kit = Switch(view, row);
            Assert.That(kit.Toggle.isOn, Is.EqualTo(on), $"{row} value");
            Assert.That(kit.Position, Is.EqualTo(on ? 1f : 0f), $"{row} knob");
            Assert.That(kit.Track.GetComponent<ThemedGraphic>().Role, Is.EqualTo(on ? ThemeRole.Primary : ThemeRole.Control), $"{row} track");
        }

        [Test]
        public void Show_puts_each_switch_where_its_value_is()
        {
            var view = View();

            view.Show(true, false, true);

            AssertSwitch(view, "Music", true);
            AssertSwitch(view, "Sfx", false);
            AssertSwitch(view, "Vibration", true);

            view.Show(false, true, false);

            AssertSwitch(view, "Music", false);
            AssertSwitch(view, "Sfx", true);
            AssertSwitch(view, "Vibration", false);
        }

        [Test]
        public void Show_does_not_raise_the_switch_events()
        {
            var view = View();
            var raised = 0;
            view.MusicToggled += _ => raised++;
            view.SfxToggled += _ => raised++;
            view.VibrationToggled += _ => raised++;

            view.Show(false, false, false);

            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void A_message_takes_the_footers_place_and_gives_it_back()
        {
            var view = View();
            bool Shown(string part) => Find(view, $"Footer/{part}").gameObject.activeSelf;
            view.ShowLinks(true, true);
            Assert.That((Shown("Privacy"), Shown("Dot"), Shown("Terms"), Shown("Version"), Shown("Message")), Is.EqualTo((true, true, true, true, false)));

            view.ShowMessage("purchases.restored");

            Assert.That((Shown("Privacy"), Shown("Dot"), Shown("Terms"), Shown("Version"), Shown("Message")), Is.EqualTo((false, false, false, false, true)));

            view.ShowMessage(null);

            Assert.That((Shown("Privacy"), Shown("Dot"), Shown("Terms"), Shown("Version"), Shown("Message")), Is.EqualTo((true, true, true, true, false)));
        }

        [Test]
        public void A_link_without_a_url_stays_hidden_after_a_message()
        {
            var view = View();
            bool Shown(string part) => Find(view, $"Footer/{part}").gameObject.activeSelf;
            view.ShowLinks(false, true);
            view.ShowMessage("purchases.restored");

            view.ShowMessage(null);

            Assert.That((Shown("Privacy"), Shown("Dot"), Shown("Terms"), Shown("Version")), Is.EqualTo((false, false, true, true)));
        }

        [Test]
        public void Every_language_row_has_a_ring_and_only_the_chosen_one_is_filled()
        {
            var view = View();
            var languages = new[] { ("en", "English"), ("tr", "Türkçe") };
            const string list = "Languages/List/Viewport/Content";
            bool Ring(string row) => Find(view, $"{list}/{row}/Mark").gameObject.activeSelf;
            bool Filled(string row) => Find(view, $"{list}/{row}/Mark/Fill").gameObject.activeSelf;

            view.ShowLanguages(languages, "tr", "Türkçe");

            Assert.That(view.LanguagesOpen, Is.True, "page open");
            // The card's own background layer is also named Body (KitCard): reach the content's Body through its Rows
            Assert.That(Find(view, "Body/Rows").parent.GetComponent<CanvasGroup>().alpha, Is.Zero, "body covered");
            foreach (var row in new[] { "Device", "en", "tr" }) Assert.That(Ring(row), Is.True, $"{row} ring");
            Assert.That((Filled("Device"), Filled("en"), Filled("tr")), Is.EqualTo((false, false, true)));

            view.ShowLanguages(languages, null, "Türkçe");

            Assert.That((Filled("Device"), Filled("en"), Filled("tr")), Is.EqualTo((true, false, false)));

            view.HideLanguages();

            Assert.That(view.LanguagesOpen, Is.False, "page closed");
            Assert.That(Find(view, "Body/Rows").parent.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "body back");
        }
    }
}
