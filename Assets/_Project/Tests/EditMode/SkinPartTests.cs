using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The version 2 ui parts of a game skin on the kit: pills, badges, the coin icon, the controls, the progress bar, the
    // stars and the score digits. Without a skin (or with a version 1 one) every part draws as the kit always did.
    public sealed class SkinPartTests
    {
        private static readonly Color PillText = new Color32(0x2B, 0x3A, 0x1F, 0xFF);

        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;
        private Scene scene;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            // A game Compass dressed ships a skin: these tests start without one either way
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
        }

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private FakeThemeSource Theme()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                palette.Set(role, new Color(0.2f, (int)role / 20f, 0.8f, 1f));
            made.Add(palette);
            return new FakeThemeSource { Current = palette };
        }

        // SkinSprites.V2 (a pixel pack; json: another) with its sprites made here
        private GameSkin Skin(string json = null)
        {
            var sprites = SkinSprites.MakeV2(made);
            var skin = GameSkin.Parse(json ?? SkinSprites.V2, path => sprites.TryGetValue(path, out var sprite) ? sprite : null, _ => null);
            Assert.That(skin, Is.Not.Null);
            GameSkin.ForTests(skin);
            return skin;
        }

        private static ThemedGraphic Role(Graphic graphic, ThemeRole role)
        {
            var themed = graphic.gameObject.AddComponent<ThemedGraphic>();
            themed.SetRole(role);
            return themed;
        }

        // A pill as SceneBuilder.Pill builds it (Surface, round ends) with a Text label, carrying a chip
        private KitChip Chip(out TMP_Text label)
        {
            var root = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(240f, 84f);
            var body = root.GetComponent<Image>();
            Role(body, ThemeRole.Surface);
            KitShape.Round(body, KitSprites.Pill);
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            label = text.GetComponent<TMP_Text>();
            Role(label, ThemeRole.Text);
            var chip = root.AddComponent<KitChip>();
            chip.Init(body, new[] { label });
            return chip;
        }

        [Test]
        public void Chip_takes_the_skin_pill_and_text_colour()
        {
            var skin = Skin();
            var chip = Chip(out var label);
            var theme = Theme();
            chip.Bind(theme);

            Assert.That(chip.Body.sprite, Is.SameAs(skin.Pill.Sprite));
            Assert.That(chip.Body.color, Is.EqualTo(Color.white), "in its own colours");
            Assert.That(chip.Body.type, Is.EqualTo(Image.Type.Tiled), "a pixel pack tiles");
            Assert.That(chip.Body.pixelsPerUnitMultiplier, Is.EqualTo(1f / skin.SliceScale).Within(1e-5f));
            Assert.That(chip.Body.GetComponent<KitPill>().enabled, Is.False, "no pill fit on a sprite");
            Assert.That(label.color, Is.EqualTo(PillText));

            theme.Switch(theme.Current);
            Assert.That(chip.Body.color, Is.EqualTo(Color.white), "after a repaint");
            Assert.That(label.color, Is.EqualTo(PillText), "label after a repaint");
        }

        [Test]
        public void Chip_without_a_pill_sprite_keeps_its_role()
        {
            Skin(SkinSprites.V2.Replace(@"""sprite"":""Art/sample/pill"",", ""));
            var chip = Chip(out var label);
            var theme = Theme();
            chip.Bind(theme);

            Assert.That(chip.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(KitShape.RadiusOf(chip.Body), Is.EqualTo(KitSprites.Pill), "a pill as built");
            Assert.That(chip.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)));
            Assert.That(label.color, Is.EqualTo(PillText));
        }

        [Test]
        public void Chip_without_a_skin_is_unchanged()
        {
            foreach (var (name, json) in new[] { ("no skin", (string)null), ("version 1", SkinSprites.Json) })
            {
                if (json == null) GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
                else Skin(json);
                var chip = Chip(out var label);
                var theme = Theme();
                chip.Bind(theme);

                Assert.That(chip.Body.sprite, Is.SameAs(KitSprites.Rounded), name);
                Assert.That(KitShape.RadiusOf(chip.Body), Is.EqualTo(KitSprites.Pill), name);
                Assert.That(chip.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)), name);
                Assert.That(chip.Body.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Surface), name);
                Assert.That(label.color, Is.EqualTo(theme.Current.Get(ThemeRole.Text)), name);
                Assert.That(label.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Text), name);
            }
        }

        [Test]
        public void Badge_takes_the_skin_pill()
        {
            var plain = KitFactory.Badge(canvas.transform, "Plain", KitBadgeTone.Accent);
            var theme = Theme();
            plain.Bind(theme);
            Assert.That(plain.Body.sprite, Is.SameAs(KitSprites.Rounded), "without a skin");
            Assert.That(plain.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)));
            Assert.That(plain.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnAccent)));

            var skin = Skin();
            var badge = KitFactory.Badge(canvas.transform, "Badge", KitBadgeTone.Accent);
            badge.Bind(theme);
            Assert.That(badge.Body.sprite, Is.SameAs(skin.Pill.Sprite));
            Assert.That(badge.Body.color, Is.EqualTo(Color.white));
            Assert.That(badge.Label.color, Is.EqualTo(PillText));
        }

        // A fallback icon as SceneBuilder.MakeFallbackIcon builds it: an Accent glyph, an Accent disc, an OnAccent letter
        private FallbackIcon Icon(out Image glyph, out Image disc, out GameObject letter)
        {
            var root = new GameObject("Icon", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Image Layer(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                var image = go.GetComponent<Image>();
                Role(image, ThemeRole.Accent);
                return image;
            }
            glyph = Layer("Glyph");
            disc = Layer("Disc");
            letter = new GameObject("Letter", typeof(RectTransform), typeof(TextMeshProUGUI));
            letter.transform.SetParent(root.transform, false);
            Role(letter.GetComponent<TMP_Text>(), ThemeRole.OnAccent);
            var localized = letter.AddComponent<LocalizedText>();
            var icon = root.AddComponent<FallbackIcon>();
            var wiring = new SerializedObject(icon);
            wiring.FindProperty("glyph").objectReferenceValue = glyph;
            wiring.FindProperty("disc").objectReferenceValue = disc;
            wiring.FindProperty("letter").objectReferenceValue = localized;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            var theme = Theme();
            foreach (var themed in root.GetComponentsInChildren<ThemedGraphic>(true)) themed.Bind(theme);
            return icon;
        }

        [Test]
        public void Coin_icon_comes_from_the_skin()
        {
            var gold = new Color(1f, 0.8f, 0.2f);
            var plain = Icon(out var plainGlyph, out var plainDisc, out _);
            plain.ShowPlain();
            Assert.That((plainGlyph.enabled, plainDisc.gameObject.activeSelf), Is.EqualTo((false, true)), "without a skin: the disc");
            plain.ShowCurrency(new CurrencyMark(KitSprites.Star, gold, "G"));
            Assert.That(plainGlyph.color, Is.EqualTo(gold), "without a skin: the tinted glyph");

            var skin = Skin();
            var coin = skin.Icon("coin");
            Assert.That(coin, Is.Not.Null);
            var icon = Icon(out var glyph, out var disc, out var letter);

            icon.ShowPlain();
            AssertCoin(icon, glyph, disc, letter, coin, "plain");

            icon.ShowCurrency(new CurrencyMark(null, gold, "G"));
            AssertCoin(icon, glyph, disc, letter, coin, "a currency without an icon");

            icon.ShowCurrency(new CurrencyMark(KitSprites.Star, gold, "G"));
            Assert.That((glyph.sprite, glyph.color), Is.EqualTo((KitSprites.Star, gold)), "a currency's own icon stays");

            icon.ShowName(null, "booster");
            Assert.That((glyph.enabled, disc.gameObject.activeSelf, letter.activeSelf), Is.EqualTo((false, true, true)), "names keep the disc");
        }

        private static void AssertCoin(FallbackIcon icon, Image glyph, Image disc, GameObject letter, Sprite coin, string message)
        {
            Assert.That(icon.UsesFallback, Is.False, message);
            Assert.That(glyph.enabled, Is.True, message);
            Assert.That(glyph.sprite, Is.SameAs(coin), message);
            Assert.That(glyph.color, Is.EqualTo(Color.white), message);
            var rim = glyph.GetComponent<Outline>();
            Assert.That(rim == null || !rim.enabled, Is.True, $"{message}: no rim");
            Assert.That(disc.gameObject.activeSelf, Is.False, $"{message}: no disc");
            Assert.That(letter.activeSelf, Is.False, $"{message}: no letter");
        }

        [Test]
        public void Slider_parts_take_their_sprites()
        {
            var skin = Skin();
            var kit = KitFactory.Slider(canvas.transform, "Slider");
            kit.Bind(Theme());

            Assert.That(kit.Rail.sprite, Is.SameAs(skin.Slider.Track));
            Assert.That(kit.Fill.sprite, Is.SameAs(skin.Slider.Fill));
            Assert.That(kit.Handle.sprite, Is.SameAs(skin.Slider.Knob));
            foreach (var part in new[] { kit.Rail, kit.Fill, kit.Handle })
                Assert.That(part.color, Is.EqualTo(Color.white), $"{part.name} in its own colours");
            Assert.That(kit.Handle.preserveAspect, Is.True, "the knob keeps its aspect");
            Assert.That(kit.Handle.type, Is.EqualTo(Image.Type.Simple), "the knob is drawn whole");
            Assert.That(kit.Dot.gameObject.activeSelf, Is.False, "no dot on a knob sprite");
            Assert.That(kit.HandleShadow.gameObject.activeSelf, Is.False, "no shadow under a knob sprite");
        }

        [Test]
        public void Slider_part_without_a_sprite_keeps_its_role()
        {
            var skin = Skin(SkinSprites.V2.Replace(@"""track"":""Art/sample/slider_track"",""fill"":""Art/sample/slider_fill"",", ""));
            Assert.That((skin.Slider.Track, skin.Slider.Fill), Is.EqualTo(((Sprite)null, (Sprite)null)), "fixture: knob only");
            var kit = KitFactory.Slider(canvas.transform, "Slider");
            var theme = Theme();
            kit.Bind(theme);

            Assert.That(kit.Rail.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(KitShape.RadiusOf(kit.Rail), Is.EqualTo(KitSprites.Pill));
            Assert.That(kit.Rail.color, Is.EqualTo(theme.Current.Get(ThemeRole.Control)));
            Assert.That(kit.Fill.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(KitShape.RadiusOf(kit.Fill), Is.EqualTo(KitSprites.Pill));
            Assert.That(kit.Fill.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
            Assert.That(kit.Handle.sprite, Is.SameAs(skin.Slider.Knob));
            Assert.That(kit.Handle.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void Switch_track_darkens_when_off()
        {
            var skin = Skin();
            var kit = KitFactory.Switch(canvas.transform, "Switch");
            var theme = Theme();
            kit.Bind(theme);

            Assert.That(kit.Track.sprite, Is.SameAs(skin.Switch.Track));
            Assert.That(kit.Track.color, Is.EqualTo(Color.white), "on");
            Assert.That(kit.Knob.sprite, Is.SameAs(skin.Switch.Knob));
            Assert.That(kit.Knob.color, Is.EqualTo(Color.white), "knob in its own colours");
            Assert.That(kit.Knob.preserveAspect, Is.True);
            Assert.That(kit.KnobShadow.gameObject.activeSelf, Is.False, "no shadow under a knob sprite");

            kit.Toggle.isOn = false;
            theme.Switch(theme.Current);
            Assert.That(kit.Track.color, Is.EqualTo(ThemedGraphic.Tone(Color.white, KitSwitch.OffShade, 1f)), "off");

            kit.Toggle.isOn = true;
            theme.Switch(theme.Current);
            Assert.That(kit.Track.color, Is.EqualTo(Color.white), "on again");
        }

        [Test]
        public void Segmented_indicator_takes_the_primary_button()
        {
            var skin = Skin();
            var primary = skin.Button(KitButtonVariant.Primary);
            var kit = KitFactory.Segmented(canvas.transform, "Picker", 3);
            var theme = Theme();
            kit.Bind(theme);
            kit.Select(1, false);

            Assert.That(kit.Indicator.sprite, Is.SameAs(primary.Normal));
            Assert.That(kit.Indicator.color, Is.EqualTo(Color.white));
            Assert.That(kit.Label(1).color, Is.EqualTo(primary.Text), "the chosen label");
            foreach (var i in new[] { 0, 2 })
            {
                Assert.That(kit.Label(i).GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Text), $"label {i}");
                Assert.That(kit.Label(i).color, Is.EqualTo(theme.Current.Get(ThemeRole.Text)), $"label {i}");
            }
            Assert.That(kit.Track.sprite, Is.SameAs(KitSprites.Rounded), "the track stays palette-drawn");
            Assert.That(kit.Track.color, Is.EqualTo(theme.Current.Get(ThemeRole.Track)));
        }

        [Test]
        public void Progress_fills_to_its_value()
        {
            var kit = KitFactory.Progress(canvas.transform, "Progress");
            kit.Bind(Theme());

            kit.SetValue(0.3f);
            Assert.That(kit.Value, Is.EqualTo(0.3f));
            Assert.That(kit.Fill.gameObject.activeSelf, Is.True);
            Assert.That(kit.Fill.rectTransform.anchorMin.x, Is.EqualTo(0f));
            Assert.That(kit.Fill.rectTransform.anchorMax.x, Is.EqualTo(0.3f).Within(1e-5f));

            kit.SetValue(2f);
            Assert.That(kit.Value, Is.EqualTo(1f), "clamped");
            Assert.That(kit.Fill.rectTransform.anchorMax.x, Is.EqualTo(1f));
            Assert.That(kit.Fill.rectTransform.rect.width, Is.EqualTo(kit.Track.rectTransform.rect.width).Within(0.01f), "full");

            kit.SetValue(-1f);
            Assert.That(kit.Value, Is.EqualTo(0f), "clamped");
            Assert.That(kit.Fill.gameObject.activeSelf, Is.False, "hidden at 0");
        }

        [Test]
        public void Progress_takes_its_sprites()
        {
            var skin = Skin();
            var kit = KitFactory.Progress(canvas.transform, "Progress");
            kit.Bind(Theme());

            Assert.That(kit.Track.sprite, Is.SameAs(skin.Progress.Track));
            Assert.That(kit.Fill.sprite, Is.SameAs(skin.Progress.Fill));
            Assert.That((kit.Track.color, kit.Fill.color), Is.EqualTo((Color.white, Color.white)));

            kit.SetValue(0.001f);
            var border = skin.Progress.Fill.border;
            var least = (border.x + border.z) * skin.SliceScale;
            Assert.That(kit.Fill.gameObject.activeSelf, Is.True);
            Assert.That(kit.Fill.rectTransform.rect.width, Is.GreaterThanOrEqualTo(least - 0.01f), "never narrower than its frame");
        }

        [Test]
        public void Controls_without_a_skin_are_unchanged()
        {
            foreach (var (name, json) in new[] { ("no skin", (string)null), ("version 1", SkinSprites.Json) })
            {
                if (json == null) GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
                else Skin(json);
                var theme = Theme();

                var slider = KitFactory.Slider(canvas.transform, "Slider");
                slider.Bind(theme);
                AssertPill(slider.Rail, ThemeRole.Control, theme, $"{name}: rail");
                AssertPill(slider.Fill, ThemeRole.Primary, theme, $"{name}: fill");
                AssertPill(slider.Handle, ThemeRole.Primary, theme, $"{name}: handle");
                AssertPill(slider.Dot, ThemeRole.OnPrimary, theme, $"{name}: dot");
                Assert.That(slider.Dot.gameObject.activeSelf, Is.True, $"{name}: dot shown");
                AssertShadow(slider.HandleShadow, $"{name}: handle shadow");

                var toggle = KitFactory.Switch(canvas.transform, "Switch");
                toggle.Bind(theme);
                AssertPill(toggle.Track, ThemeRole.Primary, theme, $"{name}: switch track on");
                AssertPill(toggle.Knob, ThemeRole.Surface, theme, $"{name}: knob");
                AssertShadow(toggle.KnobShadow, $"{name}: knob shadow");
                toggle.Toggle.isOn = false;
                theme.Switch(theme.Current);
                AssertPill(toggle.Track, ThemeRole.Control, theme, $"{name}: switch track off");

                var picker = KitFactory.Segmented(canvas.transform, "Picker", 3);
                picker.Bind(theme);
                AssertPill(picker.Track, ThemeRole.Track, theme, $"{name}: segmented track");
                AssertPill(picker.Indicator, ThemeRole.Primary, theme, $"{name}: indicator");
                Assert.That(picker.Label(0).color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), $"{name}: chosen label");
                Assert.That(picker.Label(1).color, Is.EqualTo(theme.Current.Get(ThemeRole.Text)), $"{name}: other label");

                var progress = KitFactory.Progress(canvas.transform, "Progress");
                progress.Bind(theme);
                progress.SetValue(0.5f);
                AssertPill(progress.Track, ThemeRole.Track, theme, $"{name}: progress track");
                AssertPill(progress.Fill, ThemeRole.Primary, theme, $"{name}: progress fill");
            }
        }

        [Test]
        public void Controls_drop_the_skin_when_it_goes()
        {
            Skin();
            var theme = Theme();
            var slider = KitFactory.Slider(canvas.transform, "Slider");
            var toggle = KitFactory.Switch(canvas.transform, "Switch");
            var picker = KitFactory.Segmented(canvas.transform, "Picker", 2);
            var progress = KitFactory.Progress(canvas.transform, "Progress");
            foreach (var kit in new KitElement[] { slider, toggle, picker, progress }) kit.Bind(theme);

            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            theme.Switch(theme.Current);

            AssertPill(slider.Handle, ThemeRole.Primary, theme, "slider handle");
            Assert.That(slider.Handle.preserveAspect, Is.False, "slider handle aspect");
            Assert.That(slider.Dot.gameObject.activeSelf, Is.True, "slider dot");
            AssertShadow(slider.HandleShadow, "slider shadow");
            AssertPill(toggle.Track, ThemeRole.Primary, theme, "switch track");
            AssertPill(toggle.Knob, ThemeRole.Surface, theme, "switch knob");
            AssertShadow(toggle.KnobShadow, "switch shadow");
            AssertPill(picker.Indicator, ThemeRole.Primary, theme, "indicator");
            Assert.That(picker.Label(0).color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), "chosen label");
            AssertPill(progress.Track, ThemeRole.Track, theme, "progress track");
        }

        private static void AssertPill(Image image, ThemeRole role, FakeThemeSource theme, string message)
        {
            Assert.That(image.sprite, Is.SameAs(KitSprites.Rounded), message);
            Assert.That(KitShape.RadiusOf(image), Is.EqualTo(KitSprites.Pill), message);
            Assert.That(image.GetComponent<ThemedGraphic>().Role, Is.EqualTo(role), message);
            Assert.That(image.color, Is.EqualTo(theme.Current.Get(role)), message);
        }

        private static void AssertShadow(Image shadow, string message)
        {
            Assert.That(shadow.gameObject.activeSelf, Is.True, message);
            Assert.That(shadow.sprite, Is.SameAs(KitSprites.Shadow), message);
            Assert.That(shadow.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Scrim), message);
        }

        // A level card's star row as SceneBuilder.BuildStarRow builds it: Track slots under Accent fills
        private StarRow Stars(out Image[] slots, out Image[] fills)
        {
            var root = new GameObject("Stars", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            slots = new Image[3];
            fills = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var star = new GameObject($"Star{i}", typeof(RectTransform));
                star.transform.SetParent(root.transform, false);
                slots[i] = new GameObject("Slot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                slots[i].transform.SetParent(star.transform, false);
                Role(slots[i], ThemeRole.Track);
                fills[i] = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                fills[i].transform.SetParent(star.transform, false);
                Role(fills[i], ThemeRole.Accent);
            }
            var row = root.AddComponent<StarRow>();
            var wiring = new SerializedObject(row);
            foreach (var (name, images) in new[] { ("slots", slots), ("fills", fills) })
            {
                var property = wiring.FindProperty(name);
                property.arraySize = images.Length;
                for (var i = 0; i < images.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        [Test]
        public void Stars_take_the_skin_sprites()
        {
            var plain = Stars(out var plainSlots, out var plainFills);
            var theme = Theme();
            plain.Bind(theme);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(plainSlots[i].sprite, Is.SameAs(KitSprites.Star), $"without a skin: slot {i}");
                Assert.That(plainFills[i].sprite, Is.SameAs(KitSprites.Star), $"without a skin: fill {i}");
                Assert.That(plainSlots[i].color, Is.EqualTo(theme.Current.Get(ThemeRole.Track)), $"without a skin: slot {i}");
                Assert.That(plainFills[i].color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)), $"without a skin: fill {i}");
            }

            var skin = Skin();
            var row = Stars(out var slots, out var fills);
            row.Bind(theme);
            theme.Switch(theme.Current);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(slots[i].sprite, Is.SameAs(skin.Star.Empty), $"slot {i}");
                Assert.That(fills[i].sprite, Is.SameAs(skin.Star.Full), $"fill {i}");
                Assert.That((slots[i].color, fills[i].color), Is.EqualTo((Color.white, Color.white)), $"star {i} in its own colours");
                Assert.That((slots[i].preserveAspect, fills[i].preserveAspect), Is.EqualTo((true, true)), $"star {i} keeps its aspect");
            }

            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            theme.Switch(theme.Current);
            Assert.That(slots[0].sprite, Is.SameAs(KitSprites.Star), "the skin gone");
            Assert.That(slots[0].color, Is.EqualTo(theme.Current.Get(ThemeRole.Track)), "the skin gone");
            Assert.That(fills[0].color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)), "the skin gone");
        }

        // A score count (300 x 180, "{0:0}") with its digits stretched over it, as SceneBuilder.BuildShellHud builds it
        private CountUpText Score()
        {
            var count = KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 132);
            ((RectTransform)count.transform).sizeDelta = new Vector2(300f, 180f);
            var digits = new GameObject("Digits", typeof(RectTransform)).AddComponent<KitDigits>();
            var rect = (RectTransform)digits.transform;
            rect.SetParent(count.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            count.UseDigits(digits);
            count.Bind(Theme());
            return count;
        }

        private static void AssertDigits(CountUpText count, Dictionary<string, Sprite> sprites, string number)
        {
            Assert.That(count.Digits.gameObject.activeSelf, Is.True, number);
            Assert.That(count.Digits.Count, Is.EqualTo(number.Length), number);
            for (var i = 0; i < number.Length; i++)
            {
                Assert.That(count.Digits.Digit(i).sprite, Is.SameAs(sprites[$"Art/sample/hud{number[i]}"]), $"{number}: digit {i}");
                Assert.That(count.Digits.Digit(i).gameObject.activeSelf, Is.True, $"{number}: digit {i}");
            }
            Assert.That(count.GetComponent<TMP_Text>().enabled, Is.False, $"{number}: no text under the digits");
        }

        // SkinSprites.V2 parsed over sprites (returned) made here
        private Dictionary<string, Sprite> DigitSkin()
        {
            var sprites = SkinSprites.MakeV2(made);
            GameSkin.ForTests(GameSkin.Parse(SkinSprites.V2, path => sprites.TryGetValue(path, out var sprite) ? sprite : null, _ => null));
            Assert.That(GameSkin.Current.Digits, Is.Not.Null, "fixture: digits");
            return sprites;
        }

        [Test]
        public void Score_uses_the_skin_digits()
        {
            var sprites = DigitSkin();
            var score = Score();

            score.SetValue(305);
            AssertDigits(score, sprites, "305");

            score.SetValue(7);
            AssertDigits(score, sprites, "7");
            Assert.That(score.Digits.Digit(1) == null || !score.Digits.Digit(1).gameObject.activeSelf, Is.True, "a shorter number hides the rest");
        }

        [Test]
        public void Score_follows_the_count()
        {
            var sprites = DigitSkin();
            var score = Score();

            score.CountTo(12, 0.3f);
            score.Advance(1f);
            Assert.That(score.Value, Is.EqualTo(12));
            AssertDigits(score, sprites, "12");
        }

        // A 100 x 100 digit sprite drawn only in columns 30..69 (the rest transparent, as a pack's digits are), tight mesh
        private Sprite PaddedDigit(int index)
        {
            var texture = new Texture2D(100, 100, TextureFormat.RGBA32, false) { name = $"padded{index}" };
            var pixels = new Color32[100 * 100];
            for (var y = 0; y < 100; y++)
                for (var x = 30; x < 70; x++)
                    pixels[y * 100 + x] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 100f, 100f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.Tight);
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        [Test]
        public void Digits_sit_side_by_side()
        {
            var digits = new GameObject("Digits", typeof(RectTransform)).AddComponent<KitDigits>();
            var box = (RectTransform)digits.transform;
            box.SetParent(canvas.transform, false);
            box.sizeDelta = new Vector2(600f, 180f);
            var sprites = Enumerable.Range(0, 10).Select(PaddedDigit).ToArray();

            digits.Show(42, sprites);

            Assert.That(digits.Count, Is.EqualTo(2));
            var four = digits.Digit(0).rectTransform;
            var two = digits.Digit(1).rectTransform;
            var height = four.rect.height;
            // The drawn columns of each digit in the box's space
            float Left(RectTransform digit) => digit.anchoredPosition.x - digit.rect.width * 0.5f + digit.rect.width * 0.3f;
            float Right(RectTransform digit) => digit.anchoredPosition.x - digit.rect.width * 0.5f + digit.rect.width * 0.7f;
            var gap = Left(two) - Right(four);
            Assert.That(gap, Is.InRange(-0.01f, height * 0.1f), "the visible digits close together");
            Assert.That(Left(four) + Right(two), Is.EqualTo(0f).Within(0.01f), "centred on what is drawn");
            Assert.That(height, Is.EqualTo(180f).Within(0.01f), "as tall as the box");
        }

        [Test]
        public void Non_score_formats_keep_the_text()
        {
            DigitSkin();
            var score = Score();
            var text = score.GetComponent<TMP_Text>();

            score.SetValue(-5);
            Assert.That((text.enabled, score.Digits.gameObject.activeSelf), Is.EqualTo((true, false)), "a negative value");

            score.SetValue(5);
            score.SetFormat("+{0:0}");
            Assert.That((text.enabled, score.Digits.gameObject.activeSelf), Is.EqualTo((true, false)), "a gain");
            Assert.That(text.text, Is.EqualTo("+5"));
        }

        [Test]
        public void Score_without_digits_keeps_the_display_font()
        {
            foreach (var (name, json) in new[] { ("no skin", (string)null), ("version 1", SkinSprites.Json) })
            {
                if (json == null) GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
                else Skin(json);
                var score = Score();
                var text = score.GetComponent<TMP_Text>();

                score.SetValue(305);
                Assert.That(text.enabled, Is.True, name);
                Assert.That(text.text, Is.EqualTo("305"), name);
                Assert.That(score.Digits.gameObject.activeSelf, Is.False, $"{name}: digits hidden");
            }
        }

        private Transform OpenCanvas(string path)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return scene.GetRootGameObjects().Single(r => r.name == "Canvas").transform;
        }

        private static void AssertChip(Transform canvas, string parent, string pill, params string[] labels)
        {
            var found = canvas.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == pill && t.parent.name == parent);
            Assert.That(found, Is.Not.Null, $"{parent}/{pill}");
            var chip = found.GetComponent<KitChip>();
            Assert.That(chip, Is.Not.Null, $"{pill} has a chip");
            Assert.That(chip.Body, Is.SameAs(found.GetComponent<Image>()), $"{pill} body");
            Assert.That(chip.Labels.Select(l => l.name), Is.EqualTo(labels), $"{pill} labels");
        }

        [Test]
        public void Built_pills_carry_a_chip()
        {
            AssertChip(OpenCanvas("Assets/_Project/Scenes/Menu.unity"), "TopBar", "Coins", "Count");
            EditorSceneManager.CloseScene(scene, true);
            // cmpose:sample
            var game = OpenCanvas("Assets/_Project/Scenes/Game.unity");
            AssertChip(game, "Items", "CounterPill", "Counter");
            AssertChip(game, "Content", "LevelPill", "Label");
            // /cmpose:sample
        }
        // cmpose:sample

        // The Game scene's shell HUD bound as SceneUiBinding does, its counter icon (FallbackIcon) and theme out
        private ShellHudView OpenHud(out FallbackIcon icon, out FakeThemeSource theme)
        {
            var canvas = OpenCanvas("Assets/_Project/Scenes/Game.unity");
            theme = Theme();
            foreach (var themed in canvas.GetComponentsInChildren<ThemedGraphic>(true)) themed.Bind(theme);
            var view = canvas.GetComponentInChildren<ShellHudView>(true);
            Assert.That(view, Is.Not.Null, "shell HUD");
            icon = (FallbackIcon)new SerializedObject(view).FindProperty("counterIcon").objectReferenceValue;
            Assert.That(icon, Is.Not.Null, "counter icon");
            return view;
        }

        [Test]
        public void Hud_counter_shows_the_skin_coin_without_an_economy_icon()
        {
            var coin = Skin().Icon("coin");
            var view = OpenHud(out var icon, out _);

            view.Show(1);
            AssertCoin(icon, icon.Glyph, icon.Disc, icon.Letter.gameObject, coin, "no economy icon");

            view.ShowCounterIcon(new CurrencyMark(KitSprites.Star, Color.red, "G"));
            view.Show(2);
            Assert.That((icon.Glyph.sprite, icon.Glyph.color), Is.EqualTo((KitSprites.Star, Color.red)), "the economy's icon wins");
        }

        [Test]
        public void Seven_digit_score_fits_its_box()
        {
            var sprites = DigitSkin();
            var view = OpenHud(out _, out _);
            var score = (CountUpText)new SerializedObject(view).FindProperty("score").objectReferenceValue;
            Assert.That(score.Digits, Is.Not.Null, "the HUD's score has digits");
            var box = (RectTransform)score.transform;
            Assert.That(box.rect.size, Is.EqualTo(new Vector2(300f, 180f)), "the HUD's score box");

            score.SetValue(1234567);
            AssertDigits(score, sprites, "1234567");
            var corners = new Vector3[4];
            var tolerance = 0.01f;
            for (var i = 0; i < score.Digits.Count; i++)
            {
                score.Digits.Digit(i).rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = box.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(box.rect.xMin - tolerance, box.rect.xMax + tolerance), $"digit {i} across");
                    Assert.That(local.y, Is.InRange(box.rect.yMin - tolerance, box.rect.yMax + tolerance), $"digit {i} up");
                }
            }
            var first = score.Digits.Digit(0).rectTransform;
            var last = score.Digits.Digit(score.Digits.Count - 1).rectTransform;
            Assert.That(last.anchoredPosition.x - first.anchoredPosition.x, Is.GreaterThan(0f), "side by side, left to right");
        }

        [Test]
        public void Hud_counter_keeps_the_disc_without_a_skin()
        {
            var view = OpenHud(out var icon, out var theme);

            view.Show(1);
            Assert.That(icon.UsesFallback, Is.True);
            Assert.That((icon.Glyph.enabled, icon.Disc.gameObject.activeSelf, icon.Letter.gameObject.activeSelf), Is.EqualTo((false, true, false)));
            Assert.That(icon.Disc.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Accent));
            Assert.That(icon.Disc.color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)), "the Accent disc");
        }
        // /cmpose:sample
    }
}
