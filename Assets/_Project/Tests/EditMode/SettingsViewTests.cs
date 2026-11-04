using System.Collections.Generic;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            KitTextOutline.DestroyMaterialsForTests();
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

        // In a chunky family the card's title sits on a band; the language page veils it and puts Back and its own title
        // where the band was, so the band goes while the page is open and comes back with the card's page
        [Test]
        public void Language_page_takes_the_band_off_and_back()
        {
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            var theme = new FakeThemeSource { Current = palette, Style = LayoutStage.TallStyle(made) };
            var root = LayoutStage.Prefab(SettingsPrefab, LayoutStage.Screens[0].Screen, theme, made);
            var view = root.GetComponent<SettingsView>();
            var card = view.transform.Find(Card).GetComponent<KitCard>();
            // The stage shows every part: the language page closes the way the view closes it
            view.HideLanguages();
            LayoutStage.Layout(root);
            Assert.That(card.Header, Is.SameAs(Find(view, "Title")), "the card's title is its header");
            Assert.That(card.Banded, Is.True, "the card's page: banded");

            view.ShowLanguages(new List<(string, string)> { ("en", "English") }, "en", "English");
            Assert.That(card.Banded, Is.False, "the language page: no band");
            Assert.That(Find(view, "Title").GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Text), "the language page: the title as built");

            view.HideLanguages();
            Assert.That(card.Banded, Is.True, "back on the card's page: banded");
        }

        // A canvas the size its CanvasScaler gives screen (match 0.5 of the 1080 x 1920 reference), drawn in world space
        private static void Size(RectTransform canvas, Vector2 screen)
        {
            var reference = canvas.GetComponent<CanvasScaler>().referenceResolution;
            var scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(screen.x / reference.x, 2f), Mathf.Log(screen.y / reference.y, 2f), 0.5f));
            canvas.localScale = Vector3.one;
            canvas.anchorMin = canvas.anchorMax = canvas.pivot = new Vector2(0.5f, 0.5f);
            canvas.sizeDelta = screen / scale;
        }

        // One frame's UI update as a device runs it: the canvas laid out, then the canvas update (layout, post-layout,
        // graphics)
        private static void Frame(RectTransform canvas)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);
            Canvas.ForceUpdateCanvases();
        }

        // The settings panel as a game opens it, nothing calling into the band: the prefab with its saved rects (its
        // column not laid out), its panel hidden (Awake), bound to a chunky style while hidden (UiBinder binds at
        // start), a frame drawn, then opened through the view and laid out by one frame
        private (SettingsView View, KitCard Card, RectTransform Canvas) OpenedLikeAGame(Vector2 screen)
        {
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            var theme = new FakeThemeSource { Current = palette, Style = LayoutStage.TallStyle(made) };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefab);
            var root = Object.Instantiate(prefab);
            made.Add(root);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvas = (RectTransform)root.transform;
            Size(canvas, screen);
            var view = root.GetComponent<SettingsView>();
            view.Hide();
            foreach (var element in root.GetComponentsInChildren<KitElement>(true)) element.Bind(theme);
            foreach (var graphic in root.GetComponentsInChildren<ThemedGraphic>(true)) graphic.Bind(theme);
            foreach (var text in root.GetComponentsInChildren<ThemedText>(true)) text.Bind(theme);
            Canvas.ForceUpdateCanvases();
            view.Show(true, true, true);
            Frame(canvas);
            return (view, view.transform.Find(Card).GetComponent<KitCard>(), canvas);
        }

        // The first open is banded: the band is placed after the canvas lays the column out, not from the saved rects
        [Test]
        public void First_open_puts_the_title_on_its_band()
        {
            var (_, card, _) = OpenedLikeAGame(new Vector2(1080f, 1920f));
            Assert.That(card.Header, Is.Not.Null, "the card's title is its header");
            Assert.That(card.Banded, Is.True, "banded on the first open");
        }

        // Turned to landscape the footer moves into the title's row, so the band goes (the footer never sits on it);
        // turned back to portrait it is there again
        [Test]
        public void Band_follows_the_card_through_a_turn()
        {
            var (view, card, canvas) = OpenedLikeAGame(new Vector2(1080f, 1920f));
            Assert.That(card.Banded, Is.True, "portrait");

            Size(canvas, new Vector2(1920f, 1080f));
            for (var i = 0; i < 3; i++) Frame(canvas);
            var title = LayoutStage.World((RectTransform)Find(view, "Title"));
            var footer = LayoutStage.World((RectTransform)Find(view, "Footer"));
            Assert.That(footer.yMax, Is.EqualTo(title.yMax).Within(1f), "landscape: the footer in the title's row");
            Assert.That(card.Banded, Is.False, "landscape: no band under the footer");
            Assert.That(card.Body.rectTransform.offsetMax.y, Is.EqualTo(-KitCard.PaletteFrame).Within(0.01f), "landscape: the plain frame");

            Size(canvas, new Vector2(1080f, 1920f));
            for (var i = 0; i < 3; i++) Frame(canvas);
            Assert.That(card.Banded, Is.True, "portrait again");
        }
    }
}
