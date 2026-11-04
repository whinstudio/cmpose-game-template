using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class KitTests
    {
        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

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
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            KitTextOutline.DestroyMaterialsForTests();
        }

        private Palette MakePalette(float seed)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                palette.Set(role, new Color(seed, (int)role / 20f, 1f - seed, 1f));
            made.Add(palette);
            return palette;
        }

        private ThemeStyle MakeStyle(float radius, SurfaceStyle surface, float shadow = 0.4f, MotionProfile motion = MotionProfile.Snappy,
            string family = "clean")
        {
            var style = ScriptableObject.CreateInstance<ThemeStyle>();
            var serialized = new SerializedObject(style);
            serialized.FindProperty("familyId").stringValue = family;
            serialized.FindProperty("cornerRadius").floatValue = radius;
            serialized.FindProperty("surface").enumValueIndex = (int)surface;
            serialized.FindProperty("shadowStrength").floatValue = shadow;
            serialized.FindProperty("motion").enumValueIndex = (int)motion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            made.Add(style);
            return style;
        }

        private FakeThemeSource Theme(ThemeStyle style) => new FakeThemeSource { Current = MakePalette(0.2f), Style = style };

        private static Image Part(Component kit, string name) => kit.transform.Find(name).GetComponent<Image>();

        // The skin of SkinSprites.Json (a pixel pack; json: another) with its sprites made here; font: the skin's font
        // source (null: none)
        private GameSkin Skin(Font font = null, string json = null)
        {
            var sprites = new Dictionary<string, Sprite>();
            var skin = GameSkin.Parse(json ?? SkinSprites.Json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = SkinSprites.Make(made, path.Substring(path.LastIndexOf('/') + 1));
                return sprite;
            }, _ => font);
            GameSkin.ForTests(skin);
            return skin;
        }

        [Test]
        public void Kit_sprites_load_from_resources_as_nine_slices()
        {
            Assert.That(KitSprites.Rounded, Is.Not.Null, "Resources/Kit/Rounded: run KitSpriteBuilder.BuildKitSprites");
            Assert.That(KitSprites.Shadow, Is.Not.Null, "Resources/Kit/Shadow: run KitSpriteBuilder.BuildKitSprites");
            Assert.That(KitSprites.Rounded.border, Is.EqualTo(Vector4.one * KitSprites.RoundedRadius));
            Assert.That(KitSprites.Shadow.border, Is.EqualTo(Vector4.one * (KitSprites.ShadowRadius + KitSprites.ShadowBlur)));
            Assert.That(KitSprites.Rounded.pixelsPerUnit, Is.EqualTo(100f));
            Assert.That(KitSprites.Shadow.pixelsPerUnit, Is.EqualTo(100f));
            foreach (var (sprite, name) in new[]
                     {
                         (KitSprites.Star, "Star"), (KitSprites.Gear, "Gear"), (KitSprites.Info, "Info"), (KitSprites.Close, "Close"),
                         (KitSprites.Back, "Back"), (KitSprites.Check, "Check"), (KitSprites.Music, "Music"), (KitSprites.Sound, "Sound"),
                         (KitSprites.Vibration, "Vibration"), (KitSprites.Globe, "Globe")
                     })
                Assert.That(sprite, Is.Not.Null, $"Resources/Kit/{name}: run KitSpriteBuilder.BuildKitSprites");
        }

        [Test]
        public void ButtonUsesStyleRadius()
        {
            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(Theme(MakeStyle(28f, SurfaceStyle.Soft)));

            Assert.That(button.Radius, Is.EqualTo(28f).Within(0.01f));
            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.Button.targetGraphic, Is.SameAs(button.Body));
        }

        [Test]
        public void Button_radius_follows_the_variant()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Flat));
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon);
            var play = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Play);
            icon.Bind(theme);
            play.Bind(theme);

            Assert.That(icon.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(play.Radius, Is.EqualTo(32f).Within(0.01f));
        }

        // Outside the clean family every button stands raised; Primary, Secondary, Play and Danger are pills like Icon and Close
        [Test]
        public void Chunky_buttons_are_raised_pills()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright"));
            foreach (var variant in new[] { KitButtonVariant.Primary, KitButtonVariant.Secondary, KitButtonVariant.Play, KitButtonVariant.Icon,
                         KitButtonVariant.Danger, KitButtonVariant.Close })
            {
                var button = KitFactory.Button(canvas.transform, variant.ToString(), variant);
                button.Bind(theme);
                Assert.That(button.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f), $"{variant}: pill");
                Assert.That(Part(button, "Edge").gameObject.activeSelf, Is.True, $"{variant}: raised base");
                Assert.That(Part(button, "Shine").gameObject.activeSelf, Is.True, $"{variant}: shine");
            }
        }

        // A chunky Secondary is a raised SurfaceVariant pill: its base is its own colour darker, not a Control outline
        [Test]
        public void Chunky_secondary_is_surface_variant_without_outline()
        {
            var secondary = KitFactory.Button(canvas.transform, "Secondary", KitButtonVariant.Secondary);
            secondary.Bind(Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright")));

            Assert.That(secondary.Body.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.SurfaceVariant), "body");
            Assert.That(secondary.Label.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Text), "label");
            Assert.That(Part(secondary, "Edge").GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.SurfaceVariant), "raised base");
        }

        [Test]
        public void Clean_buttons_keep_the_style_radius()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "clean"));
            var primary = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            var secondary = KitFactory.Button(canvas.transform, "Secondary", KitButtonVariant.Secondary);
            primary.Bind(theme);
            secondary.Bind(theme);

            Assert.That(primary.Radius, Is.EqualTo(28f).Within(0.01f));
            Assert.That(Part(secondary, "Edge").GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control), "outline");
        }

        // A destructive action (leaving a run) is red in every family: a Danger body, an OnPrimary label in the body font,
        // bold; a raised pill when chunky, the style's radius in clean
        [Test]
        public void Danger_button_is_red_with_light_label()
        {
            foreach (var (family, radius) in new[] { ("bright", KitSprites.Pill), ("clean", 28f) })
            {
                var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: family));
                var button = KitFactory.Button(canvas.transform, $"Danger {family}", KitButtonVariant.Danger);
                button.Bind(theme);

                Assert.That(button.Body.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Danger), $"{family}: body role");
                Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Danger)), $"{family}: body colour");
                Assert.That(button.Label.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.OnPrimary), $"{family}: label role");
                Assert.That(button.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), $"{family}: label colour");
                Assert.That(KitButton.LabelFont(KitButtonVariant.Danger), Is.EqualTo(TextRole.Body), $"{family}: LabelFont");
                Assert.That(button.Label.GetComponent<ThemedText>().Role, Is.EqualTo(TextRole.Body), $"{family}: label font");
                Assert.That(button.Label.fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Bold), $"{family}: bold");
                Assert.That(button.Radius, Is.EqualTo(radius).Within(0.01f), $"{family}: radius");
                Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(KitFactory.ButtonSize), $"{family}: a labelled button's size");
            }
        }

        // A card's close is a round red X in every family: a Danger circle, its glyph in OnPrimary on the middle (no disc)
        [Test]
        public void Close_button_is_a_round_red_x()
        {
            foreach (var family in new[] { "bright", "clean" })
            {
                var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: family));
                var close = KitFactory.Button(canvas.transform, $"Close {family}", KitButtonVariant.Close, KitSprites.Close);
                close.Bind(theme);

                Assert.That(close.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f), $"{family}: round");
                Assert.That(KitButton.RadiusFor(KitButtonVariant.Close, 28f), Is.EqualTo(KitSprites.Pill), $"{family}: RadiusFor");
                Assert.That(close.Body.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Danger), $"{family}: body role");
                Assert.That(close.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Danger)), $"{family}: body colour");
                Assert.That(close.Icon, Is.Not.Null, $"{family}: glyph");
                Assert.That(close.Icon.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.OnPrimary), $"{family}: glyph role");
                Assert.That(close.Icon.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), $"{family}: glyph colour");
                Assert.That(close.transform.Find("IconBack"), Is.Null, $"{family}: the glyph fills the middle, no disc");
                Assert.That(close.Icon.transform.parent, Is.SameAs(close.transform), $"{family}: the glyph on the button");
                Assert.That(((RectTransform)close.transform).sizeDelta, Is.EqualTo(KitFactory.IconSize), $"{family}: a round button's size");
            }
        }

        // A skin has no Danger or Close sprites: Danger wears its Primary sprite, Close its Icon sprite (as the settings X
        // did); a skin without an Icon entry leaves the X in the kit's red circle
        [Test]
        public void Skinned_danger_and_close_wear_the_primary_and_icon_sprites()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            Assert.That(skin.Button(KitButtonVariant.Danger), Is.SameAs(skin.Button(KitButtonVariant.Primary)), "Danger entry");
            Assert.That(skin.Button(KitButtonVariant.Close), Is.SameAs(skin.Button(KitButtonVariant.Icon)), "Close entry");
            var danger = KitFactory.Button(canvas.transform, "Danger", KitButtonVariant.Danger);
            var close = KitFactory.Button(canvas.transform, "Close", KitButtonVariant.Close, KitSprites.Close);
            danger.Bind(theme);
            close.Bind(theme);
            Assert.That(danger.Body.sprite, Is.SameAs(skin.Button(KitButtonVariant.Primary).Normal), "Danger sprite");
            Assert.That(close.Body.sprite, Is.SameAs(skin.Button(KitButtonVariant.Icon).Normal), "Close sprite");

            GameSkin.ForTests(GameSkin.Parse(SkinSprites.WithoutIcon, path => SkinSprites.Make(made, path), _ => null));
            var plain = KitFactory.Button(canvas.transform, "Plain", KitButtonVariant.Close, KitSprites.Close);
            plain.Bind(theme);
            Assert.That(plain.Body.sprite, Is.SameAs(KitSprites.Rounded), "no Icon entry: the kit's circle");
            Assert.That(plain.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Danger)), "no Icon entry: red");
        }

        private static void Near(Vector2 actual, Vector2 expected, string what)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f), $"{what} (x)");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f), $"{what} (y)");
        }

        // A style of the bright family whose body font is the font at path
        private ThemeStyle BrightWith(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Assert.That(font, Is.Not.Null, path);
            var style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright");
            var serialized = new SerializedObject(style);
            serialized.FindProperty("bodyFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return style;
        }

        private const string InterFont = "Assets/_Project/Core/Presentation/Fonts/Inter/Inter-Regular SDF.asset";
        private const string MontserratFont = "Assets/_Project/Core/Presentation/Fonts/Montserrat/Montserrat-Bold SDF.asset";

        private static void AssertOutline(TMP_Text label, Color colour, string what)
        {
            Assert.That(KitTextOutline.Has(label), Is.True, $"{what}: outlined");
            var material = label.fontSharedMaterial;
            // A material keeps its colours in the project's colour space: back within a rounding
            var drawn = material.GetColor(ShaderUtilities.ID_OutlineColor);
            for (var i = 0; i < 4; i++)
                Assert.That(drawn[i], Is.EqualTo(colour[i]).Within(1e-4f), $"{what}: outline colour [{i}]");
            Assert.That(material.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(0.12f).Within(1e-5f), $"{what}: outline width");
            Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"), Is.False, $"{what}: no underlay");
            Assert.That(material.GetTexture(ShaderUtilities.ID_MainTex), Is.SameAs(label.font.material.GetTexture(ShaderUtilities.ID_MainTex)),
                $"{what}: on its font's atlas");
        }

        // A chunky family's light labels on Primary, Play and Danger are outlined in their body's colour, darker (shade
        // 0.45), 0.12 wide, no underlay; Secondary (dark text on a light body) and the clean family are not
        [Test]
        public void Chunky_labels_are_outlined_but_secondary()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var buttons = new List<KitButton>();
            foreach (var variant in new[] { KitButtonVariant.Primary, KitButtonVariant.Play, KitButtonVariant.Danger, KitButtonVariant.Secondary })
            {
                var button = KitFactory.Button(canvas.transform, variant.ToString(), variant);
                button.Bind(theme);
                buttons.Add(button);
            }
            void Check(string when)
            {
                foreach (var button in buttons)
                {
                    var what = $"{when} {button.Variant}";
                    if (button.Variant == KitButtonVariant.Secondary)
                    {
                        Assert.That(KitTextOutline.Has(button.Label), Is.False, $"{what}: no outline");
                        continue;
                    }
                    var body = theme.Current.Get(KitButton.BodyRole(button.Variant, true));
                    AssertOutline(button.Label, ThemedGraphic.Tone(body, 0.45f, 1f), what);
                }
            }
            Check("bright");
            theme.Switch(MakePalette(0.7f));
            Check("another palette");

            var clean = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f));
            foreach (var variant in new[] { KitButtonVariant.Primary, KitButtonVariant.Play, KitButtonVariant.Danger, KitButtonVariant.Secondary })
            {
                var button = KitFactory.Button(canvas.transform, $"Clean {variant}", variant);
                button.Bind(clean);
                Assert.That(KitTextOutline.Has(button.Label), Is.False, $"clean {variant}: no outline");
            }
        }

        // A new font resets TMP's material: the outline follows the label onto the new font's own copy, whether the label is
        // bound alone or with its button
        [Test]
        public void Outline_survives_a_font_change()
        {
            var first = Theme(BrightWith(InterFont));
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            button.Bind(first);
            var colour = ThemedGraphic.Tone(first.Current.Get(ThemeRole.Primary), 0.45f, 1f);
            Assert.That(button.Label.font.name, Does.StartWith("Inter"), "first font");
            AssertOutline(button.Label, colour, "first font");

            var second = new FakeThemeSource { Current = first.Current, Style = BrightWith(MontserratFont) };
            button.Label.GetComponent<ThemedText>().Bind(second);
            Assert.That(button.Label.font.name, Does.StartWith("Montserrat"), "second font");
            AssertOutline(button.Label, colour, "the label bound alone");

            button.Bind(first);
            button.Bind(second);
            AssertOutline(button.Label, colour, "bound with its button");
        }

        // While the finger is down on a chunky button its body, shine, label and glyph sit down on the raised edge (by the
        // edge's depth: 6 + 10 x shadow strength), so the edge no longer shows; up, they are back where they were
        [Test]
        public void Pressing_a_chunky_button_hides_its_edge()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary, KitSprites.Gear);
            button.Bind(theme);
            var depth = 6f + 10f * 0.4f;
            var edge = Part(button, "Edge").rectTransform;
            var shine = Part(button, "Shine").rectTransform;
            var body = button.Body.rectTransform;
            var label = button.Label.rectTransform;
            var glyph = (RectTransform)button.transform.Find("IconBack");
            Assert.That(glyph, Is.Not.Null, "the glyph's disc");
            var before = (edge.offsetMin, edge.offsetMax, body.offsetMin, body.offsetMax, shine.offsetMin, shine.offsetMax,
                label.anchoredPosition, glyph.anchoredPosition);
            Assert.That(edge.offsetMin.y, Is.EqualTo(-depth).Within(1e-4f), "the edge shows below the body");
            Assert.That(button.Pressed, Is.False, "up");

            button.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Pressed, Is.True, "down");
            Assert.That(body.offsetMin.y, Is.EqualTo(edge.offsetMin.y).Within(1e-4f), "the body covers the edge: bottom");
            Assert.That(body.offsetMax.y, Is.EqualTo(edge.offsetMax.y).Within(1e-4f), "the body covers the edge: top");
            Assert.That(body.offsetMin.x, Is.EqualTo(before.Item3.x), "the body does not move sideways");
            Near(edge.offsetMin, before.Item1, "the edge stays: min");
            Near(edge.offsetMax, before.Item2, "the edge stays: max");
            Assert.That(shine.offsetMin.y, Is.EqualTo(before.Item5.y - depth).Within(1e-4f), "shine bottom");
            Assert.That(shine.offsetMax.y, Is.EqualTo(before.Item6.y - depth).Within(1e-4f), "shine top");
            Assert.That(label.anchoredPosition.y, Is.EqualTo(before.Item7.y - depth).Within(1e-4f), "label");
            Assert.That(label.anchoredPosition.x, Is.EqualTo(before.Item7.x).Within(1e-4f), "label x");
            Assert.That(glyph.anchoredPosition.y, Is.EqualTo(before.Item8.y - depth).Within(1e-4f), "glyph");

            button.OnPointerDown(new PointerEventData(null));
            Assert.That(body.offsetMin.y, Is.EqualTo(edge.offsetMin.y).Within(1e-4f), "a second down does not press further");

            void AssertBack(string what)
            {
                Assert.That(button.Pressed, Is.False, $"{what}: up");
                Near(body.offsetMin, before.Item3, $"{what}: body min");
                Near(body.offsetMax, before.Item4, $"{what}: body max");
                Near(shine.offsetMin, before.Item5, $"{what}: shine min");
                Near(shine.offsetMax, before.Item6, $"{what}: shine max");
                Near(label.anchoredPosition, before.Item7, $"{what}: label");
                Near(glyph.anchoredPosition, before.Item8, $"{what}: glyph");
                Near(edge.offsetMin, before.Item1, $"{what}: edge min");
                Near(edge.offsetMax, before.Item2, $"{what}: edge max");
            }
            button.OnPointerUp(new PointerEventData(null));
            AssertBack("released");
            button.OnPointerUp(new PointerEventData(null));
            AssertBack("a second up");

            // A repaint while pressed puts it back first: offsets never add up
            button.OnPointerDown(new PointerEventData(null));
            theme.Switch(MakePalette(0.6f));
            AssertBack("repainted while pressed");
            button.OnPointerUp(new PointerEventData(null));
            AssertBack("released after the repaint");

            // An uninteractable button does not press; one made uninteractable while down comes back on the finger's up
            // (uGUI sends it to the pressed object whatever its state)
            button.Button.interactable = false;
            button.OnPointerDown(new PointerEventData(null));
            AssertBack("uninteractable");
            button.Button.interactable = true;
            button.OnPointerDown(new PointerEventData(null));
            button.Button.interactable = false;
            button.OnPointerUp(new PointerEventData(null));
            AssertBack("made uninteractable while down");
        }

        // Like a Selectable's pressed state, the pressed edge follows the finger: sliding off a pressed chunky button lifts
        // it, sliding back on with the same finger presses it again, and the finger's up off the button leaves it up. A
        // finger (or a mouse) that did not press the button only passes over it
        [Test]
        public void Sliding_off_a_pressed_button_lifts_it_and_sliding_back_presses_it()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary, KitSprites.Gear);
            button.Bind(theme);
            var depth = 6f + 10f * 0.4f;
            var body = button.Body.rectTransform;
            var label = button.Label.rectTransform;
            var before = (body.offsetMin, body.offsetMax, label.anchoredPosition);
            var finger = new PointerEventData(null) { pointerPress = button.gameObject };
            var passing = new PointerEventData(null);
            // Through the event system, as the input module sends them (the Button's own handlers run too)
            void Exit(PointerEventData data) => ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
            void Enter(PointerEventData data) => ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
            void AssertUp(string what)
            {
                Assert.That(button.Pressed, Is.False, $"{what}: up");
                Near(body.offsetMin, before.Item1, $"{what}: body min");
                Near(body.offsetMax, before.Item2, $"{what}: body max");
                Near(label.anchoredPosition, before.Item3, $"{what}: label");
            }
            void AssertDown(string what)
            {
                Assert.That(button.Pressed, Is.True, $"{what}: down");
                Assert.That(body.offsetMin.y, Is.EqualTo(before.Item1.y - depth).Within(1e-4f), $"{what}: body sat down");
                Assert.That(label.anchoredPosition.y, Is.EqualTo(before.Item3.y - depth).Within(1e-4f), $"{what}: label sat down");
            }

            button.OnPointerDown(finger);
            AssertDown("pressed");
            Exit(finger);
            AssertUp("slid off");
            Exit(finger);
            AssertUp("a second exit");
            Enter(finger);
            AssertDown("slid back on");
            Enter(finger);
            AssertDown("a second enter does not press further");

            Exit(finger);
            button.OnPointerUp(finger);
            AssertUp("released off the button");
            finger.pointerPress = null;
            Enter(finger);
            AssertUp("the same finger back after its up");

            Enter(passing);
            AssertUp("a finger that did not press it");
            Exit(passing);
            AssertUp("that finger gone");

            // Slid back on after it turned uninteractable: no press
            finger.pointerPress = button.gameObject;
            button.OnPointerDown(finger);
            Exit(finger);
            button.Button.interactable = false;
            Enter(finger);
            AssertUp("uninteractable when slid back on");
            button.OnPointerUp(finger);
            AssertUp("released");
        }

        // A button watches for turning uninteractable only while it is pressed: no per-frame message on every button
        [Test]
        public void Kit_button_has_no_per_frame_message()
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (var message in new[] { "Update", "LateUpdate", "FixedUpdate" })
                Assert.That(typeof(KitButton).GetMethod(message, all), Is.Null, $"KitButton.{message}");
        }

        // A theme switch to clean while the finger is down leaves no button pressed and the label on its font's own material
        [Test]
        public void Pressed_button_comes_back_on_a_switch_to_clean()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary, KitSprites.Gear);
            button.Bind(theme);
            var body = button.Body.rectTransform;
            var shine = Part(button, "Shine").rectTransform;
            var label = button.Label.rectTransform;
            var glyph = (RectTransform)button.transform.Find("IconBack");
            var before = (body.offsetMin, body.offsetMax, label.anchoredPosition, glyph.anchoredPosition);
            Assert.That(KitTextOutline.Has(button.Label), Is.True, "outlined when chunky");

            button.OnPointerDown(new PointerEventData(null));
            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f);
            theme.Switch(theme.Current);

            Assert.That(button.Pressed, Is.False, "not pressed");
            Near(body.offsetMin, before.Item1, "body min");
            Near(body.offsetMax, before.Item2, "body max");
            Near(label.anchoredPosition, before.Item3, "label");
            Near(glyph.anchoredPosition, before.Item4, "glyph");
            Assert.That(Part(button, "Shine").gameObject.activeSelf, Is.False, "clean Soft: no shine");
            Assert.That(KitTextOutline.Has(button.Label), Is.False, "no outline in clean");
            Assert.That(button.Label.fontSharedMaterial, Is.SameAs(button.Label.font.material), "the font's own material");

            button.OnPointerUp(new PointerEventData(null));
            Near(body.offsetMin, before.Item1, "body min after the up");
            Near(body.offsetMax, before.Item2, "body max after the up");
            Near(label.anchoredPosition, before.Item3, "label after the up");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright");
            theme.Switch(theme.Current);
            Assert.That(KitTextOutline.Has(button.Label), Is.True, "outlined again when chunky again");
            Assert.That(shine.anchorMin.y, Is.EqualTo(KitShape.ShineBottom), "shine back on the top");
        }

        // The pressed edge is the chunky look's: a clean Raised button and a skinned one keep their body where it is (a skin
        // shows its pressed sprite, as before)
        [Test]
        public void Clean_and_skinned_buttons_do_not_move_their_body_when_pressed()
        {
            var clean = KitFactory.Button(canvas.transform, "Clean", KitButtonVariant.Primary);
            clean.Bind(Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f)));
            var body = clean.Body.rectTransform;
            var label = clean.Label.rectTransform;
            var before = (body.offsetMin, body.offsetMax, label.anchoredPosition);
            clean.OnPointerDown(new PointerEventData(null));
            Assert.That(clean.Pressed, Is.False, "clean: nothing drawn pressed");
            Near(body.offsetMin, before.Item1, "clean: body min");
            Near(body.offsetMax, before.Item2, "clean: body max");
            Near(label.anchoredPosition, before.Item3, "clean: label");
            clean.OnPointerUp(new PointerEventData(null));

            var skin = Skin();
            var skinned = KitFactory.Button(canvas.transform, "Skinned", KitButtonVariant.Primary);
            skinned.Bind(Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright")));
            var entry = skin.Button(KitButtonVariant.Primary);
            var skinnedBody = skinned.Body.rectTransform;
            var bodyBefore = (skinnedBody.offsetMin, skinnedBody.offsetMax);
            Assert.That(KitTextOutline.Has(skinned.Label), Is.False, "skinned: no outline");
            skinned.OnPointerDown(new PointerEventData(null));
            Assert.That(skinned.Body.sprite, Is.SameAs(entry.Pressed), "skinned: the pressed sprite");
            Assert.That(skinned.Pressed, Is.True, "skinned: drawn pressed");
            Near(skinnedBody.offsetMin, bodyBefore.Item1, "skinned: body min");
            Near(skinnedBody.offsetMax, bodyBefore.Item2, "skinned: body max");
            skinned.OnPointerUp(new PointerEventData(null));
            Assert.That(skinned.Body.sprite, Is.SameAs(entry.Normal), "skinned: the normal sprite");
            Assert.That(skinned.Pressed, Is.False, "skinned: up");
        }

        [Test]
        public void A_theme_switch_back_to_clean_undoes_the_chunky_look()
        {
            var secondary = KitFactory.Button(canvas.transform, "Secondary", KitButtonVariant.Secondary);
            var theme = new FakeThemeSource { Current = MakePalette(0.2f), Style = MakeStyle(28f, SurfaceStyle.Soft, family: "bright") };
            secondary.Bind(theme);
            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, family: "clean");
            theme.Switch(theme.Current);

            Assert.That(secondary.Radius, Is.EqualTo(28f).Within(0.01f), "radius");
            Assert.That(Part(secondary, "Shine").gameObject.activeSelf, Is.False, "shine");
            Assert.That(secondary.Body.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Surface), "body");
        }

        [Test]
        public void A_square_style_gives_square_buttons()
        {
            var button = KitFactory.Button(canvas.transform, "B", KitButtonVariant.Primary);
            button.Bind(Theme(MakeStyle(0f, SurfaceStyle.Flat)));

            Assert.That(button.Radius, Is.EqualTo(0f));
            Assert.That(button.Body.sprite, Is.Null);
        }

        [Test]
        public void Surfaces_turn_on_their_layers()
        {
            var soft = KitFactory.Button(canvas.transform, "Soft", KitButtonVariant.Primary);
            var raised = KitFactory.Button(canvas.transform, "Raised", KitButtonVariant.Primary);
            var flat = KitFactory.Button(canvas.transform, "Flat", KitButtonVariant.Primary);
            soft.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft, 0.35f)));
            raised.Bind(Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f)));
            flat.Bind(Theme(MakeStyle(20f, SurfaceStyle.Flat)));

            Assert.That(Part(soft, "Shadow").gameObject.activeSelf, Is.True, "soft: shadow");
            Assert.That(Part(soft, "Edge").gameObject.activeSelf, Is.False, "soft: edge");
            Assert.That(Part(soft, "Shadow").GetComponent<ThemedGraphic>().Opacity, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(Part(raised, "Shadow").gameObject.activeSelf, Is.False, "raised: shadow");
            var edge = Part(raised, "Edge");
            Assert.That(edge.gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(edge.rectTransform.offsetMin.y, Is.LessThan(-5f), "raised: the base shows below the body");
            Assert.That(edge.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Primary));
            Assert.That(edge.GetComponent<ThemedGraphic>().Shade, Is.GreaterThan(0f), "raised: the base is darker");
            Assert.That(Part(raised, "Shine").gameObject.activeSelf, Is.True, "raised: shine");
            foreach (var layer in new[] { "Shadow", "Edge", "Shine" })
                Assert.That(Part(flat, layer).gameObject.activeSelf, Is.False, $"flat: {layer}");
        }

        // A raised button's shine is a thin strip along the top edge, clear of the label in the middle: chunky or a Raised
        // family, one shape; its side inset grows with a tall button (12 % of the height, at least 12 units)
        [Test]
        public void A_raised_buttons_shine_is_a_thin_strip_along_the_top()
        {
            var cases = new[]
            {
                ("chunky", MakeStyle(28f, SurfaceStyle.Soft, family: "bright"), KitButtonVariant.Secondary, 110f),
                ("raised", MakeStyle(20f, SurfaceStyle.Raised, 0.5f), KitButtonVariant.Primary, 110f),
                ("tall", MakeStyle(28f, SurfaceStyle.Soft, family: "neon"), KitButtonVariant.Play, 200f)
            };
            foreach (var (what, style, variant, height) in cases)
            {
                var button = KitFactory.Button(canvas.transform, what, variant);
                ((RectTransform)button.transform).sizeDelta = new Vector2(400f, height);
                button.Bind(Theme(style));
                var shine = Part(button, "Shine");
                var rect = shine.rectTransform;
                var inset = Mathf.Max(12f, 0.12f * height);
                Assert.That(shine.gameObject.activeSelf, Is.True, $"{what}: shine");
                Assert.That(rect.anchorMin.y, Is.GreaterThanOrEqualTo(0.7f), $"{what}: a strip along the top, clear of the label");
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), $"{what}: up to the top");
                Assert.That(rect.anchorMin.x, Is.EqualTo(0f), $"{what}: side to side");
                Assert.That(rect.offsetMin.x, Is.EqualTo(inset).Within(0.01f), $"{what}: left inset");
                Assert.That(rect.offsetMax.x, Is.EqualTo(-inset).Within(0.01f), $"{what}: right inset");
                Assert.That(rect.offsetMax.y, Is.EqualTo(-6f).Within(0.01f), $"{what}: below the top edge");
                Assert.That(shine.GetComponent<ThemedGraphic>().Opacity, Is.EqualTo(0.16f).Within(0.001f), $"{what}: opacity");
                // A pill's strip is a pill too; a rounded box's strip is rounded 6 less
                var rounding = button.Radius >= KitSprites.Pill ? KitSprites.Pill : Mathf.Max(button.Radius - 6f, 0f);
                Assert.That(KitShape.RadiusOf(shine), Is.EqualTo(rounding).Within(0.5f), $"{what}: the body's rounding");
            }
        }

        // A raised Secondary's outline rims the top too (white on a white card would read as cut off), the base shows below
        [Test]
        public void A_raised_outline_rims_the_top_of_the_button()
        {
            var secondary = KitFactory.Button(canvas.transform, "Secondary", KitButtonVariant.Secondary);
            secondary.Bind(Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f)));
            var edge = Part(secondary, "Edge").rectTransform;
            Assert.That(edge.offsetMax.y, Is.GreaterThan(0f), "rims the top");
            Assert.That(edge.offsetMin.y, Is.LessThan(-5f), "the base shows below");
        }

        [Test]
        public void Variants_colour_body_and_label_with_readable_pairs()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            var expected = new Dictionary<KitButtonVariant, (ThemeRole Body, ThemeRole Label)>
            {
                { KitButtonVariant.Primary, (ThemeRole.Primary, ThemeRole.OnPrimary) },
                { KitButtonVariant.Secondary, (ThemeRole.Surface, ThemeRole.Text) },
                { KitButtonVariant.Icon, (ThemeRole.Primary, ThemeRole.OnPrimary) },
                { KitButtonVariant.Play, (ThemeRole.Accent, ThemeRole.OnAccent) }
            };
            foreach (var pair in expected)
            {
                var button = KitFactory.Button(canvas.transform, pair.Key.ToString(), pair.Key);
                button.Bind(theme);
                Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(pair.Value.Body)), $"{pair.Key}: body");
                Assert.That(button.Label.color, Is.EqualTo(theme.Current.Get(pair.Value.Label)), $"{pair.Key}: label");
            }
            var secondary = KitFactory.Button(canvas.transform, "Outlined", KitButtonVariant.Secondary);
            secondary.Bind(theme);
            Assert.That(Part(secondary, "Edge").gameObject.activeSelf, Is.True, "secondary: outline");
            Assert.That(Part(secondary, "Edge").GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control));
        }

        // A game's display font (a handwriting or pixel face) is unreadable on a button: every label but Play's takes the
        // body font, bold, skinned or not
        [Test]
        public void Button_labels_use_the_body_font_but_play()
        {
            foreach (var skinned in new[] { false, true })
            {
                if (skinned) Skin();
                var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
                foreach (var variant in new[] { KitButtonVariant.Primary, KitButtonVariant.Secondary, KitButtonVariant.Icon, KitButtonVariant.Play })
                {
                    var button = KitFactory.Button(canvas.transform, variant.ToString(), variant);
                    button.Bind(theme);
                    var where = $"{(skinned ? "skinned " : "")}{variant}";
                    var label = button.Label.GetComponent<ThemedText>();
                    var expected = variant == KitButtonVariant.Play ? TextRole.Title : TextRole.Body;
                    Assert.That(KitButton.LabelFont(variant), Is.EqualTo(expected), $"{where}: LabelFont");
                    Assert.That(label.Role, Is.EqualTo(expected), $"{where}: role");
                    if (expected == TextRole.Body)
                        Assert.That(button.Label.fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Bold), $"{where}: bold");
                }
            }
        }

        // In a sheet's panel only the titles (named Title or Subtitle) and Display texts keep the display font; the other
        // Title texts take the body font, bold; a button's label is the button's; a card of its own is left alone
        [Test]
        public void Sheet_panels_keep_the_display_font_for_titles_only()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            var title = Text(sheet.Content, "Title", TextRole.Title);
            var subtitle = Text(sheet.Content, "Subtitle", TextRole.Title);
            var amount = Text(sheet.Content, "Amount", TextRole.Title);
            var big = Text(sheet.Content, "Big", TextRole.Display);
            var body = Text(sheet.Content, "Row", TextRole.Body);
            var play = KitFactory.Button(sheet.Content, "Play", KitButtonVariant.Play);
            var own = KitFactory.Card(canvas.transform, "Own");
            var loose = Text(own.transform, "Amount", TextRole.Title);
            sheet.Bind(theme);
            own.Bind(theme);

            Assert.That(title.Role, Is.EqualTo(TextRole.Title), "Title");
            Assert.That(subtitle.Role, Is.EqualTo(TextRole.Title), "Subtitle");
            Assert.That(big.Role, Is.EqualTo(TextRole.Display), "Display");
            Assert.That(amount.Role, Is.EqualTo(TextRole.Body), "Amount");
            Assert.That(amount.GetComponent<TMP_Text>().fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Bold), "Amount: bold");
            Assert.That(body.Role, Is.EqualTo(TextRole.Body), "Row");
            Assert.That(body.GetComponent<TMP_Text>().fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Normal), "Row: as it was");
            Assert.That(play.Label.GetComponent<ThemedText>().Role, Is.EqualTo(TextRole.Title), "the play button's label is its own");
            Assert.That(loose.Role, Is.EqualTo(TextRole.Title), "a card of its own");
            Assert.That(KitCard.KeepsDisplayFont(title.transform), Is.True);
            Assert.That(KitCard.KeepsDisplayFont(big.transform), Is.True);
            Assert.That(KitCard.KeepsDisplayFont(amount.transform), Is.False);
        }

        // SceneBuilder's modals move the panel into a safe area under the sheet (Panel/SafeArea/Card): it is still the
        // sheet's panel, framed in a chunky family, its texts in one font
        [Test]
        public void A_modal_card_in_a_safe_area_is_still_its_sheets_panel()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright"));
            var sheet = KitFactory.Sheet(canvas.transform, "Panel", SheetPlacement.Center, 600f);
            var area = new GameObject("SafeArea", typeof(RectTransform));
            area.transform.SetParent(sheet.transform, false);
            sheet.Panel.name = "Card";
            sheet.Panel.SetParent(area.transform, false);
            var amount = Text(sheet.Content, "Amount", TextRole.Title);
            sheet.Bind(theme);

            Assert.That(sheet.Panel.GetComponent<KitCard>().Framed, Is.True, "framed");
            Assert.That(amount.Role, Is.EqualTo(TextRole.Body), "one font");
        }

        private static ThemedText Text(Transform parent, string name, TextRole role)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var themed = go.AddComponent<ThemedText>();
            themed.SetRole(role);
            return themed;
        }

        [Test]
        public void Play_breathes_and_every_button_springs()
        {
            var play = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Play);
            var primary = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);

            Assert.That(play.GetComponent<IdlePulse>(), Is.Not.Null);
            Assert.That(play.GetComponent<PressFeedback>(), Is.Not.Null);
            Assert.That(primary.GetComponent<IdlePulse>(), Is.Null);
            Assert.That(primary.GetComponent<PressFeedback>(), Is.Not.Null);
        }

        // The glyph sits in its disc at the button's left, clear of the label; Secondary draws it in Primary (3:1 on
        // Surface in every family), the others in their label colour
        [Test]
        public void An_icon_button_shows_its_glyph_beside_the_label()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            foreach (var variant in new[] { KitButtonVariant.Secondary, KitButtonVariant.Primary })
            {
                var button = KitFactory.Button(canvas.transform, variant.ToString(), variant, KitSprites.Gear);
                button.Bind(theme);
                Assert.That(button.Icon, Is.Not.Null, $"{variant}: icon");
                Assert.That(button.Icon.sprite, Is.SameAs(KitSprites.Gear), $"{variant}: sprite");
                Assert.That(button.Icon.raycastTarget, Is.False, $"{variant}: the icon takes no touches");
                var role = variant == KitButtonVariant.Secondary ? ThemeRole.Primary : ThemeRole.OnPrimary;
                Assert.That(button.Icon.color, Is.EqualTo(theme.Current.Get(role)), $"{variant}: icon colour");
                var back = Part(button, "IconBack");
                Assert.That(back.GetComponent<ThemedGraphic>().Role, Is.EqualTo(role), $"{variant}: disc role");
                Assert.That(back.GetComponent<ThemedGraphic>().Opacity, Is.EqualTo(KitButton.IconBackOpacity), $"{variant}: disc is faint");
                var discRight = back.rectTransform.anchoredPosition.x + back.rectTransform.sizeDelta.x / 2f;
                Assert.That(button.Label.margin.x, Is.GreaterThan(discRight), $"{variant}: the label starts after the disc");
                Assert.That(button.Label.transform.GetSiblingIndex(), Is.EqualTo(button.transform.childCount - 1), $"{variant}: label on top");
            }
            Assert.That(KitFactory.Button(canvas.transform, "Plain", KitButtonVariant.Secondary).Icon, Is.Null, "no icon by default");
        }

        // Decor is background: faint, untouchable, themed, in the shape asked for
        [Test]
        public void Decor_is_a_faint_untouchable_themed_shape()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            foreach (KitDecorShape shape in Enum.GetValues(typeof(KitDecorShape)))
            {
                var decor = KitFactory.Decor(canvas.transform, shape.ToString(), shape, ThemeRole.Accent, 0.2f, 120f, 15f);
                decor.Bind(theme);
                var image = decor.GetComponent<Image>();
                Assert.That(image.raycastTarget, Is.False, $"{shape}: no touches");
                Assert.That(image.color, Is.EqualTo(ThemedGraphic.Tone(theme.Current.Get(ThemeRole.Accent), 0f, 0.2f)), $"{shape}: colour");
                Assert.That(decor.Shape, Is.EqualTo(shape));
                Assert.That(image.sprite, Is.SameAs(shape == KitDecorShape.Star ? KitSprites.Star : KitSprites.Rounded), $"{shape}: sprite");
                Assert.That(decor.transform.localEulerAngles.z, Is.EqualTo(15f).Within(0.01f), $"{shape}: tilt");
            }
        }

        // A drifting shape comes back to where it was placed when it stops
        [Test]
        public void Decor_drifts_around_its_place_and_returns_to_it()
        {
            var decor = KitFactory.Decor(canvas.transform, "Circle", KitDecorShape.Circle, ThemeRole.Primary, 0.1f, 200f, 0f, 20f);
            decor.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            var rect = (RectTransform)decor.transform;
            rect.anchoredPosition = new Vector2(10f, 30f);
            var lowest = float.MaxValue;
            var highest = float.MinValue;
            for (var i = 0; i < 200; i++)
            {
                decor.Advance(0.05f);
                lowest = Mathf.Min(lowest, rect.anchoredPosition.y);
                highest = Mathf.Max(highest, rect.anchoredPosition.y);
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(10f), "drifts only up and down");
            }
            Assert.That(highest, Is.EqualTo(50f).Within(0.5f), "up by its drift");
            Assert.That(lowest, Is.EqualTo(10f).Within(0.5f), "down by its drift");
            decor.Rest();
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(10f, 30f)), "back in place");
        }

        [Test]
        public void Every_kit_graphic_has_a_theme_role()
        {
            var parts = new Component[]
            {
                KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Secondary),
                KitFactory.Card(canvas.transform, "Card"),
                KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Bottom, 900f),
                KitFactory.Switch(canvas.transform, "Switch"),
                KitFactory.Slider(canvas.transform, "Slider"),
                KitFactory.Segmented(canvas.transform, "Segmented", 3),
                KitFactory.Progress(canvas.transform, "Progress"),
                KitFactory.Badge(canvas.transform, "Badge", KitBadgeTone.Primary),
                KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 96f),
                KitFactory.Button(canvas.transform, "IconButton", KitButtonVariant.Secondary, KitSprites.Info),
                KitFactory.Decor(canvas.transform, "Decor", KitDecorShape.Star, ThemeRole.Accent, 0.5f, 60f)
            };
            foreach (var part in parts)
            foreach (var graphic in part.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.GetComponent<ThemedGraphic>(), Is.Not.Null, $"{part.name}/{graphic.name}");
        }

        [Test]
        public void SwitchRepaintsOnThemeChange()
        {
            var kit = KitFactory.Switch(canvas.transform, "Switch");
            var first = MakePalette(0.1f);
            var second = MakePalette(0.9f);
            var theme = new FakeThemeSource { Current = first, Style = MakeStyle(20f, SurfaceStyle.Soft) };
            kit.Bind(theme);
            Assert.That(kit.Track.color, Is.EqualTo(first.Get(ThemeRole.Primary)), "on, first palette");
            Assert.That(kit.Knob.color, Is.EqualTo(first.Get(ThemeRole.Surface)), "knob, first palette");

            theme.Switch(second);
            Assert.That(kit.Track.color, Is.EqualTo(second.Get(ThemeRole.Primary)), "on, second palette");
            Assert.That(kit.Knob.color, Is.EqualTo(second.Get(ThemeRole.Surface)), "knob, second palette");

            kit.Toggle.isOn = false;
            theme.Switch(first);
            Assert.That(kit.Track.color, Is.EqualTo(first.Get(ThemeRole.Control)), "off, first palette");
            Assert.That(kit.Position, Is.EqualTo(0f));
        }

        [Test]
        public void Slider_parts_are_round_and_the_fill_ends_under_the_handle()
        {
            var kit = KitFactory.Slider(canvas.transform, "Slider");
            kit.Bind(Theme(MakeStyle(14f, SurfaceStyle.Raised)));

            Assert.That(KitShape.RadiusOf(kit.Rail), Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(KitShape.RadiusOf(kit.Handle), Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(kit.Slider.fillRect, Is.SameAs(kit.Fill.rectTransform));
            Assert.That(kit.Fill.rectTransform.offsetMax.x, Is.EqualTo(0f));
            Assert.That(kit.Rail.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control));
            Assert.That(kit.Handle.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Primary));
        }

        [Test]
        public void CountUpReachesTarget()
        {
            var count = KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 96f);
            var reached = 0;
            count.Reached += () => reached++;

            count.CountTo(250, 0.5f);
            count.Advance(0.25f);
            Assert.That(count.Value, Is.GreaterThan(0).And.LessThan(250), "half way");
            Assert.That(reached, Is.Zero);

            count.Advance(0.3f);
            Assert.That(count.Value, Is.EqualTo(250));
            Assert.That(count.Counting, Is.False);
            Assert.That(count.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("250"));
            Assert.That(reached, Is.EqualTo(1));

            count.Advance(1f);
            Assert.That(reached, Is.EqualTo(1), "reaching is announced once");
            Assert.That(count.transform.localScale, Is.EqualTo(Vector3.one), "the pop settles");
        }

        [Test]
        public void CountUp_counts_down_and_set_value_is_immediate()
        {
            var count = KitFactory.CountUp(canvas.transform, "Coins", TextRole.Title, 64f);
            count.SetValue(90);
            Assert.That(count.Value, Is.EqualTo(90));
            Assert.That(count.Counting, Is.False);

            count.CountTo(40, 0.2f);
            count.Advance(0.1f);
            Assert.That(count.Value, Is.InRange(41, 89));
            count.Advance(0.2f);
            Assert.That(count.Value, Is.EqualTo(40));
        }

        [Test]
        public void MotionDurationsFollowProfile()
        {
            var calm = UiMotion.Timings(MotionProfile.Calm);
            var snappy = UiMotion.Timings(MotionProfile.Snappy);
            var bouncy = UiMotion.Timings(MotionProfile.Bouncy);

            Assert.That(calm.Transition, Is.GreaterThan(bouncy.Transition));
            Assert.That(bouncy.Transition, Is.GreaterThan(snappy.Transition));
            Assert.That(calm.Sheet, Is.GreaterThan(bouncy.Sheet));
            Assert.That(bouncy.Sheet, Is.GreaterThan(snappy.Sheet));
            Assert.That(calm.Press, Is.GreaterThan(snappy.Press));
            Assert.That(calm.Breath, Is.GreaterThan(snappy.Breath));
            Assert.That(snappy.Breath, Is.GreaterThan(bouncy.Breath));
            Assert.That(bouncy.PressScale, Is.LessThan(snappy.PressScale));
            Assert.That(snappy.PressScale, Is.LessThan(calm.PressScale));
            Assert.That(bouncy.ReleaseEase, Is.EqualTo(Ease.Spring));
            Assert.That(calm.ReleaseEase, Is.EqualTo(Ease.OutCubic));
            // A paused game still animates: nothing in the kit takes more than half a second, except counting
            foreach (var t in new[] { calm, snappy, bouncy })
            {
                Assert.That(t.Press + t.Release, Is.LessThanOrEqualTo(0.5f));
                Assert.That(t.Sheet, Is.LessThanOrEqualTo(0.5f));
            }
            Assert.That(UiMotion.ProfileOf(null), Is.EqualTo(MotionProfile.Snappy));
            Assert.That(UiMotion.ProfileOf(Theme(MakeStyle(20f, SurfaceStyle.Soft, motion: MotionProfile.Calm))), Is.EqualTo(MotionProfile.Calm));
        }

        [Test]
        public void Every_ease_starts_at_zero_and_ends_at_one()
        {
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
            {
                Assert.That(UiMotion.Evaluate(ease, 0f), Is.EqualTo(0f), $"{ease} at 0");
                Assert.That(UiMotion.Evaluate(ease, 1f), Is.EqualTo(1f), $"{ease} at 1");
                Assert.That(UiMotion.Evaluate(ease, -1f), Is.EqualTo(0f), $"{ease} before");
                Assert.That(UiMotion.Evaluate(ease, 2f), Is.EqualTo(1f), $"{ease} after");
            }
            var peak = 0f;
            for (var t = 0f; t <= 1f; t += 0.01f) peak = Mathf.Max(peak, UiMotion.Evaluate(Ease.Spring, t));
            Assert.That(peak, Is.InRange(1.1f, 1.3f), "the spring overshoots");
            Assert.That(UiMotion.Evaluate(Ease.OutCubic, 0.5f), Is.LessThan(1f), "OutCubic does not");
            Assert.That(UiMotion.Punch(0f), Is.EqualTo(0f));
            Assert.That(UiMotion.Punch(1f), Is.EqualTo(0f));
        }

        [Test]
        public void A_tween_steps_to_its_target_and_rests()
        {
            var tween = new KitTween();
            tween.Start(0f, 1f, 0.2f, Ease.Linear);
            Assert.That(tween.Step(0.1f), Is.True);
            Assert.That(tween.Value, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(tween.Step(0.2f), Is.True);
            Assert.That(tween.Value, Is.EqualTo(1f));
            Assert.That(tween.Running, Is.False);
            Assert.That(tween.Step(0.1f), Is.False);

            tween.Start(0f, 1f, 0f, Ease.Spring);
            Assert.That(tween.Running, Is.False, "a zero-length tween is at its end at once");
            Assert.That(tween.Value, Is.EqualTo(1f));
        }

        [Test]
        public void A_sheet_is_built_closed_and_opens_and_closes_at_once_without_animation()
        {
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Bottom, 900f);
            sheet.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That(sheet.IsOpen, Is.False);

            var opened = 0;
            var closed = 0;
            sheet.Opened += () => opened++;
            sheet.Closed += () => closed++;
            sheet.SetOpen(true, false);
            Assert.That(sheet.gameObject.activeSelf, Is.True);
            Assert.That(sheet.Progress, Is.EqualTo(1f));
            Assert.That(sheet.Dim.alpha, Is.EqualTo(1f));
            Assert.That(sheet.Panel.anchoredPosition.y, Is.EqualTo(-KitFactory.SheetOverhang).Within(0.01f));

            sheet.SetOpen(false, false);
            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That((opened, closed), Is.EqualTo((1, 1)));
        }

        [Test]
        public void Segmented_moves_its_indicator_and_recolours_labels()
        {
            var kit = KitFactory.Segmented(canvas.transform, "Picker", 3);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            kit.Bind(theme);

            kit.Select(2, false);
            Assert.That(kit.Selected, Is.EqualTo(2));
            Assert.That(kit.Indicator.rectTransform.anchorMin.x, Is.EqualTo(2f / 3f).Within(0.001f));
            Assert.That(kit.Label(2).color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(kit.Label(0).color, Is.EqualTo(theme.Current.Get(ThemeRole.Text)));
        }

        [Test]
        public void Segmented_refuses_bad_counts_and_indexes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => KitFactory.Segmented(canvas.transform, "One", 1));
            var kit = KitFactory.Segmented(canvas.transform, "Two", 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => kit.Select(2, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => kit.Select(-1, false));
            Assert.That(kit.Selected, Is.Zero);
        }

        [Test]
        public void A_disabled_button_does_not_press()
        {
            var kit = KitFactory.Button(canvas.transform, "Off", KitButtonVariant.Primary);
            kit.Button.interactable = false;
            var press = kit.GetComponent<PressFeedback>();

            press.OnPointerDown(new PointerEventData(null));
            Assert.That(press.Busy, Is.False);
            press.OnPointerUp(new PointerEventData(null));
            Assert.That(press.Busy, Is.False);
        }

        [Test]
        public void Badge_takes_its_tone()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            var badge = KitFactory.Badge(canvas.transform, "New", KitBadgeTone.Accent);
            badge.Bind(theme);
            badge.SetText("NEW");

            Assert.That(badge.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)));
            Assert.That(badge.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnAccent)));
            Assert.That(badge.Label.text, Is.EqualTo("NEW"));
        }

        [Test]
        public void A_tone_darkens_and_fades_the_role_colour()
        {
            var colour = new Color(0.8f, 0.6f, 0.4f, 0.5f);
            Assert.That(ThemedGraphic.Tone(colour, 0f, 1f), Is.EqualTo(colour));
            var toned = ThemedGraphic.Tone(colour, 0.5f, 0.5f);
            Assert.That(toned.r, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(toned.a, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void The_binder_binds_kit_elements()
        {
            var button = KitFactory.Button(canvas.transform, "B", KitButtonVariant.Primary);
            var theme = Theme(MakeStyle(24f, SurfaceStyle.Soft));
            new UiBinder(new Kturker.UnityBase.Core.Localization.TableLocalizer(new FakeStringSource(), new FakeLog(), false), theme).BindTree(canvas);

            Assert.That(button.Radius, Is.EqualTo(24f).Within(0.01f));
            Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
        }

        [Test]
        public void Unskinned_kit_is_unchanged()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            button.Bind(theme);
            card.Bind(theme);
            sheet.Bind(theme);

            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
            Assert.That(button.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(Part(button, "Edge").gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(Part(button, "Shine").gameObject.activeSelf, Is.True, "raised: shine");
            Assert.That(card.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(card.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)));
            var panel = sheet.Panel.GetComponent<KitCard>();
            Assert.That(panel.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(panel.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)));
            Assert.That(theme.Style.FontFor(TextRole.Display), Is.SameAs(theme.Style.DisplayFont));

            button.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded), "no pressed sprite without a skin");
        }

        // Kenney sprites keep their own colours: the body is white whatever the palette, the label takes the skin's colour
        [Test]
        public void Skinned_button_uses_sliced_sprite_and_hides_layers()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon, KitSprites.Gear);
            button.Bind(theme);
            icon.Bind(theme);

            var entry = skin.Button(KitButtonVariant.Primary);
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal));
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f));
            foreach (var layer in new[] { "Shadow", "Edge", "Shine" })
                Assert.That(Part(button, layer).gameObject.activeSelf, Is.False, layer);
            Assert.That(icon.Body.sprite, Is.SameAs(skin.Button(KitButtonVariant.Icon).Normal), "icon variant");
            Assert.That(icon.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "no pill on a skinned icon button");

            theme.Switch(MakePalette(0.9f));
            Assert.That(button.Body.color, Is.EqualTo(Color.white), "body after a palette change");
            Assert.That(button.Label.color, Is.EqualTo(entry.Text), "label after a palette change");
            Assert.That(icon.Icon.color, Is.EqualTo(skin.Button(KitButtonVariant.Icon).Text), "glyph");
        }

        // A skin without an icon entry dresses the other buttons; icon buttons keep today's round, themed layers
        [Test]
        public void Icon_button_without_skin_entry_keeps_kit_shape()
        {
            GameSkin.ForTests(GameSkin.Parse(SkinSprites.WithoutIcon, path => SkinSprites.Make(made, path), _ => null));
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon, KitSprites.Gear);
            var primary = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            icon.Bind(theme);
            primary.Bind(theme);

            Assert.That(primary.Body.sprite, Is.Not.SameAs(KitSprites.Rounded), "the primary button is dressed");
            Assert.That(icon.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(icon.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(icon.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
            Assert.That(icon.Icon.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(Part(icon, "Edge").gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(Part(icon, "Shine").gameObject.activeSelf, Is.True, "raised: shine");

            icon.OnPointerDown(new PointerEventData(null));
            Assert.That(icon.Body.sprite, Is.SameAs(KitSprites.Rounded), "no pressed sprite");
        }

        // A skinned game's buttons are its sprites: one the pack has no sprite for (here the icon button) keeps the
        // family's plain shape and surface in a chunky family too, not a raised pill among the sprites (with a skin only
        // the font rule applies)
        [Test]
        public void A_skins_palette_drawn_button_is_not_chunky()
        {
            GameSkin.ForTests(GameSkin.Parse(SkinSprites.WithoutIcon, path => SkinSprites.Make(made, path), _ => null));
            var style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright");
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon, KitSprites.Gear);
            icon.Bind(Theme(style));

            Assert.That(KitShape.Chunky(style), Is.False, "a dressed kit is not chunky");
            Assert.That(icon.Body.sprite, Is.SameAs(KitSprites.Rounded), "palette-drawn");
            Assert.That(icon.Radius, Is.EqualTo(KitButton.RadiusFor(KitButtonVariant.Icon, 28f)).Within(0.5f), "plain radius");
            Assert.That(Part(icon, "Shine").gameObject.activeSelf, Is.False, "the family's Soft surface: no shine");
            Assert.That(Part(icon, "Edge").gameObject.activeSelf, Is.False, "no raised base");
            Assert.That(Part(icon, "Shadow").gameObject.activeSelf, Is.True, "the family's soft shadow");
        }

        [Test]
        public void Skinned_button_swaps_sprite_while_pressed()
        {
            var skin = Skin();
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Secondary);
            button.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            var kit = button;
            var entry = skin.Button(KitButtonVariant.Secondary);

            kit.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Pressed), "down");
            kit.OnPointerUp(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal), "up");

            button.Button.interactable = false;
            kit.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal), "a disabled button does not press");
        }

        [Test]
        public void Skinned_card_and_dialog_use_panel_sprites()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var card = KitFactory.Card(canvas.transform, "Card", true);
            var bottom = KitFactory.Sheet(canvas.transform, "Bottom", SheetPlacement.Bottom, 900f);
            var dialog = KitFactory.Sheet(canvas.transform, "Dialog", SheetPlacement.Center, 600f);
            var inner = KitFactory.Card(dialog.Content, "Inner");
            card.Bind(theme);
            bottom.Bind(theme);
            dialog.Bind(theme);
            theme.Switch(MakePalette(0.7f));

            var cases = new[]
            {
                (card, skin.Card, "card"),
                (bottom.Panel.GetComponent<KitCard>(), skin.Sheet, "bottom sheet"),
                (dialog.Panel.GetComponent<KitCard>(), skin.Dialog, "dialog"),
                (inner, skin.Card, "a card in a dialog")
            };
            foreach (var (kit, sprite, what) in cases)
            {
                Assert.That(kit.Body.sprite, Is.SameAs(sprite), what);
                Assert.That(kit.Body.type, Is.EqualTo(Image.Type.Sliced), what);
                Assert.That(kit.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), what);
                Assert.That(kit.Body.color, Is.EqualTo(Color.white), what);
                foreach (var layer in new[] { "Shadow", "Edge" })
                    Assert.That(Part(kit, layer).gameObject.activeSelf, Is.False, $"{what}: {layer}");
            }
        }

        // A pixel pack's edges carry patterns (stitches, studs) that a stretched slice smears: drawn tiled, they repeat
        [Test]
        public void Pixel_skin_draws_tiled()
        {
            var skin = Skin();
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            button.Bind(theme);
            card.Bind(theme);

            Assert.That(skin.Pixel, Is.True);
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Tiled), "button");
            Assert.That(button.Body.fillCenter, Is.True, "button centre");
            Assert.That(button.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "button scale");
            Assert.That(card.Body.type, Is.EqualTo(Image.Type.Tiled), "card");
            Assert.That(card.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "card scale");
        }

        [Test]
        public void Soft_skin_draws_sliced()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            button.Bind(theme);
            card.Bind(theme);

            Assert.That(skin.Pixel, Is.False);
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced), "button");
            Assert.That(card.Body.type, Is.EqualTo(Image.Type.Sliced), "card");
        }

        private static void AssertOffsets(Image image, float left, float bottom, float right, float top, string what)
        {
            var rect = image.rectTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero), $"{what}: anchorMin");
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), $"{what}: anchorMax");
            Assert.That(rect.offsetMin.x, Is.EqualTo(left).Within(0.01f), $"{what}: left");
            Assert.That(rect.offsetMin.y, Is.EqualTo(bottom).Within(0.01f), $"{what}: bottom");
            Assert.That(rect.offsetMax.x, Is.EqualTo(right).Within(0.01f), $"{what}: right");
            Assert.That(rect.offsetMax.y, Is.EqualTo(top).Within(0.01f), $"{what}: top");
        }

        private static ThemedGraphic Themed(Image image) => image.GetComponent<ThemedGraphic>();

        // Outside the clean family a sheet's panel is framed: Edge is a Primary frame over the card's whole rect, Body
        // the Surface panel KitCard.PaletteFrame inside it, Shadow the frame's darker Primary base dropped below it
        [Test]
        public void Chunky_sheet_card_is_framed_in_primary()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();

            Assert.That(KitCard.PaletteFrame, Is.EqualTo(16f));
            Assert.That(card.Framed, Is.True);
            var edge = Part(card, "Edge");
            Assert.That(edge.gameObject.activeSelf, Is.True, "frame active");
            Assert.That(Themed(edge).Role, Is.EqualTo(ThemeRole.Primary), "frame role");
            Assert.That(Themed(edge).Shade, Is.EqualTo(0f), "frame shade");
            Assert.That(edge.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)), "frame colour");
            AssertOffsets(edge, 0f, 0f, 0f, 0f, "frame");
            // Panels are 1.25 x the style's radius: 35; the panel inside the frame 35 - 16
            Assert.That(KitShape.RadiusOf(edge), Is.EqualTo(35f).Within(0.01f), "frame radius");

            Assert.That(Themed(card.Body).Role, Is.EqualTo(ThemeRole.Surface), "panel role");
            Assert.That(card.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)), "panel colour");
            AssertOffsets(card.Body, 16f, 16f, -16f, -16f, "panel");
            Assert.That(card.Radius, Is.EqualTo(19f).Within(0.01f), "panel radius");

            var shadow = Part(card, "Shadow");
            var depth = 6f + 10f * 0.4f;
            Assert.That(shadow.gameObject.activeSelf, Is.True, "base active");
            Assert.That(Themed(shadow).Role, Is.EqualTo(ThemeRole.Primary), "base role");
            Assert.That(Themed(shadow).Shade, Is.EqualTo(0.35f).Within(1e-5f), "base shade");
            Assert.That(Themed(shadow).Opacity, Is.EqualTo(1f), "base opacity");
            Assert.That(shadow.sprite, Is.SameAs(KitSprites.Rounded), "base shape");
            Assert.That(KitShape.RadiusOf(shadow), Is.EqualTo(35f).Within(0.01f), "base radius");
            AssertOffsets(shadow, 0f, -depth, 0f, -depth, "base");
        }

        // The sheet's padding (48) is more than the frame plus FrameGap (16 + 12), so it stays as it is
        [Test]
        public void Framed_card_pads_content_inside_the_frame()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright"));
            var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();

            Assert.That(card.Frame, Is.EqualTo(new Vector4(16f, 16f, 16f, 16f)));
            var expected = Mathf.Max((int)KitFactory.SheetPadding, Mathf.CeilToInt(KitCard.PaletteFrame + KitCard.FrameGap));
            Assert.That(expected, Is.EqualTo(48));
            var padding = card.ContentPadding;
            Assert.That(padding.left, Is.EqualTo(expected), "left");
            Assert.That(padding.right, Is.EqualTo(expected), "right");
            Assert.That(padding.top, Is.EqualTo(expected), "top");
            Assert.That(padding.bottom, Is.EqualTo(expected), "bottom");
            Assert.That(sheet.Content.offsetMin, Is.EqualTo(new Vector2(expected, expected)), "content min");
            Assert.That(sheet.Content.offsetMax, Is.EqualTo(new Vector2(-expected, -expected)), "content max");
        }

        // Only a sheet's panel is framed, and only outside the clean family (or a theme without a style)
        [Test]
        public void Plain_card_and_clean_sheet_are_not_framed()
        {
            var bright = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright"));
            var card = KitFactory.Card(canvas.transform, "Card");
            card.Bind(bright);
            Assert.That(card.Framed, Is.False, "plain card");
            Assert.That(Themed(card.Body).Role, Is.EqualTo(ThemeRole.Surface), "plain card body");
            AssertOffsets(card.Body, 0f, 0f, 0f, 0f, "plain card body");
            Assert.That(card.Frame, Is.EqualTo(Vector4.zero), "plain card frame");

            foreach (var (theme, what) in new[] { (Theme(MakeStyle(28f, SurfaceStyle.Soft)), "clean"), (Theme(null), "no style") })
            {
                var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
                sheet.Bind(theme);
                var panel = sheet.Panel.GetComponent<KitCard>();
                Assert.That(panel.Framed, Is.False, what);
                Assert.That(panel.Frame, Is.EqualTo(Vector4.zero), $"{what}: frame");
                Assert.That(Themed(panel.Body).Role, Is.EqualTo(ThemeRole.Surface), $"{what}: body");
                AssertOffsets(panel.Body, 0f, 0f, 0f, 0f, $"{what}: body");
            }
        }

        // A skin's dialog sprite draws a chunky family's modal as before: no palette frame, the sprite's own border
        [Test]
        public void Skin_card_sprite_overrides_the_palette_frame()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, family: "bright"));
            var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();

            Assert.That(card.Framed, Is.False);
            Assert.That(card.Body.sprite, Is.SameAs(skin.Dialog));
            var reference = canvas.GetComponent<Canvas>().referencePixelsPerUnit;
            Assert.That(card.Frame, Is.EqualTo(skin.Dialog.border * (reference * skin.SliceScale / skin.Dialog.pixelsPerUnit)));
            Assert.That(card.Frame, Is.Not.EqualTo(new Vector4(16f, 16f, 16f, 16f)));
            AssertOffsets(card.Body, 0f, 0f, 0f, 0f, "body");
            foreach (var layer in new[] { "Shadow", "Edge" })
                Assert.That(Part(card, layer).gameObject.activeSelf, Is.False, layer);
        }

        [Test]
        public void Theme_switch_to_clean_unframes_the_card()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();
            Assert.That(card.Framed, Is.True, "bright");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f);
            theme.Switch(theme.Current);

            Assert.That(card.Framed, Is.False, "clean");
            Assert.That(card.Frame, Is.EqualTo(Vector4.zero), "frame");
            AssertOffsets(card.Body, 0f, 0f, 0f, 0f, "body");
            Assert.That(card.Radius, Is.EqualTo(35f).Within(0.01f), "body radius");
            Assert.That(Themed(card.Body).Role, Is.EqualTo(ThemeRole.Surface), "body role");
            // Soft: no edge, the blurred drop shadow in Scrim
            Assert.That(Part(card, "Edge").gameObject.activeSelf, Is.False, "edge");
            var shadow = Part(card, "Shadow");
            Assert.That(shadow.gameObject.activeSelf, Is.True, "soft shadow");
            Assert.That(shadow.sprite, Is.SameAs(KitSprites.Shadow), "soft shadow sprite");
            Assert.That(Themed(shadow).Role, Is.EqualTo(ThemeRole.Scrim), "soft shadow role");
            Assert.That(Themed(shadow).Shade, Is.EqualTo(0f), "soft shadow shade");
        }

        // A header title like SceneBuilder's: a Title text (ThemedText) coloured through its ThemedGraphic role, across the
        // top of parent from top units below its top edge, height units tall
        private static TMP_Text HeaderTitle(Transform parent, float top, float height, ThemeRole role = ThemeRole.Text, string name = "Title")
        {
            var text = Text(parent, name, TextRole.Title);
            text.gameObject.AddComponent<ThemedGraphic>().SetRole(role);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
            return text.GetComponent<TMP_Text>();
        }

        // An open sheet, 900 x 800, its Content inset 48 (KitFactory.SheetPadding)
        private KitSheet OpenSheet()
        {
            var sheet = KitFactory.Sheet(canvas.transform, "S", SheetPlacement.Center, 800f);
            sheet.gameObject.SetActive(true);
            return sheet;
        }

        private static void AssertTitleOutline(TMP_Text title, Palette palette, string what)
        {
            Assert.That(KitTextOutline.Has(title), Is.True, $"{what}: outlined");
            var material = title.fontSharedMaterial;
            var colour = ThemedGraphic.Tone(palette.Get(ThemeRole.Primary), 0.45f, 1f);
            var drawn = material.GetColor(ShaderUtilities.ID_OutlineColor);
            for (var i = 0; i < 4; i++)
                Assert.That(drawn[i], Is.EqualTo(colour[i]).Within(1e-4f), $"{what}: outline colour [{i}]");
            Assert.That(material.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(0.2f).Within(1e-5f), $"{what}: outline width");
            Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"), Is.True, $"{what}: underlay");
        }

        private static void AssertAsBuilt(KitCard card, TMP_Text title, ThemeRole role, float top, string what)
        {
            Assert.That(card.Banded, Is.False, $"{what}: no band");
            Assert.That(title.GetComponent<ThemedGraphic>().Role, Is.EqualTo(role), $"{what}: the title's role as built");
            Assert.That(KitTextOutline.Has(title), Is.False, $"{what}: no outline");
            Assert.That(title.fontSharedMaterial, Is.SameAs(title.font.material), $"{what}: the font's own material");
            Assert.That(card.Body.rectTransform.offsetMax.y, Is.EqualTo(top).Within(0.01f), $"{what}: panel top");
        }

        // A framed card's header (the Title right under Content, at the top) sits on the Primary frame: the Surface panel
        // starts BandGap (12) below the title's bottom edge, the title turns OnPrimary with a dark Primary outline (shade
        // 0.45, width 0.2) and an underlay
        [Test]
        public void Framed_card_puts_its_title_on_a_band()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var sheet = OpenSheet();
            var title = HeaderTitle(sheet.Content, 0f, 100f);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();

            Assert.That(KitCard.BandGap, Is.EqualTo(12f));
            Assert.That(card.Header, Is.SameAs(title.transform), "header");
            Assert.That(card.Banded, Is.True, "banded");
            // The card's top to the title's bottom: the content's padding (48) and the title (100)
            AssertOffsets(card.Body, 16f, 16f, -16f, -(48f + 100f + 12f), "panel");
            AssertOffsets(Part(card, "Edge"), 0f, 0f, 0f, 0f, "frame");
            Assert.That(title.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.OnPrimary), "title role");
            Assert.That(title.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), "title colour");
            AssertTitleOutline(title, theme.Current, "banded");
        }

        // No Title under Content: the frame as before; a clean (or skinned) card keeps its title as built
        [Test]
        public void Card_without_a_header_or_in_clean_has_no_band()
        {
            var bright = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var plain = OpenSheet();
            HeaderTitle(plain.Content, 0f, 100f, name: "Message");
            plain.Bind(bright);
            var plainCard = plain.Panel.GetComponent<KitCard>();
            Assert.That(plainCard.Header, Is.Null, "no header");
            Assert.That(plainCard.Banded, Is.False, "no band");
            AssertOffsets(plainCard.Body, 16f, 16f, -16f, -16f, "panel");

            // A header veiled (the settings card's under its language page) is no header
            var veiled = OpenSheet();
            var veiledTitle = HeaderTitle(veiled.Content, 0f, 100f);
            veiledTitle.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            veiled.Bind(bright);
            Assert.That(veiled.Panel.GetComponent<KitCard>().Header, Is.Null, "veiled: no header");
            AssertAsBuilt(veiled.Panel.GetComponent<KitCard>(), veiledTitle, ThemeRole.Text, -16f, "veiled");

            var clean = OpenSheet();
            var title = HeaderTitle(clean.Content, 0f, 100f, ThemeRole.TextMuted);
            clean.Bind(Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f)));
            var card = clean.Panel.GetComponent<KitCard>();
            Assert.That(card.Header, Is.SameAs(title.transform), "clean: the header is there");
            AssertAsBuilt(card, title, ThemeRole.TextMuted, 0f, "clean");
            AssertOffsets(card.Body, 0f, 0f, 0f, 0f, "clean panel");

            Skin(json: SkinSprites.Soft);
            var skinned = OpenSheet();
            var skinnedTitle = HeaderTitle(skinned.Content, 0f, 100f);
            skinned.Bind(bright);
            AssertAsBuilt(skinned.Panel.GetComponent<KitCard>(), skinnedTitle, ThemeRole.Text, 0f, "skinned");
        }

        // Another text or a control on the band would sit on the Primary frame: the card stays as it was (but for the
        // card's own round Close X, which overhangs its corner)
        [Test]
        public void Band_steps_aside_for_other_parts()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));

            var withText = OpenSheet();
            var title = HeaderTitle(withText.Content, 0f, 100f);
            // A coin count beside the title, inside the band's rect
            var coins = HeaderTitle(withText.Content, 20f, 60f, name: "Coins");
            ((RectTransform)coins.transform).anchorMin = new Vector2(0.8f, 1f);
            withText.Bind(theme);
            AssertAsBuilt(withText.Panel.GetComponent<KitCard>(), title, ThemeRole.Text, -16f, "a text on the band");

            // A text just below the band's bottom edge leaves it
            var below = OpenSheet();
            var belowTitle = HeaderTitle(below.Content, 0f, 100f);
            HeaderTitle(below.Content, 100f + 12f + 1f, 60f, name: "Message");
            below.Bind(theme);
            Assert.That(below.Panel.GetComponent<KitCard>().Banded, Is.True, "a text below the band");
            Assert.That(belowTitle.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.OnPrimary), "a text below the band: title role");

            var withButton = OpenSheet();
            var buttonTitle = HeaderTitle(withButton.Content, 0f, 100f);
            var back = KitFactory.Button(withButton.Content, "Back", KitButtonVariant.Icon, KitSprites.Back);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(60f, -50f);
            withButton.Bind(theme);
            AssertAsBuilt(withButton.Panel.GetComponent<KitCard>(), buttonTitle, ThemeRole.Text, -16f, "a button on the band");

            var withClose = OpenSheet();
            var closeTitle = HeaderTitle(withClose.Content, 0f, 100f);
            var close = KitFactory.Button(withClose.Panel, "Close", KitButtonVariant.Close, KitSprites.Close);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-30f, -30f);
            withClose.Bind(theme);
            Assert.That(withClose.Panel.GetComponent<KitCard>().Banded, Is.True, "the card's Close X");
            AssertTitleOutline(closeTitle, theme.Current, "the card's Close X");
        }

        // A switch to clean takes the band off (the panel's offsets, the title's role and material as built); back to a
        // chunky family it is there again
        [Test]
        public void Switch_to_clean_removes_the_band()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var sheet = OpenSheet();
            var title = HeaderTitle(sheet.Content, 0f, 100f, ThemeRole.TextMuted);
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();
            Assert.That(card.Banded, Is.True, "bright");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f);
            theme.Switch(theme.Current);
            AssertAsBuilt(card, title, ThemeRole.TextMuted, 0f, "clean");
            AssertOffsets(card.Body, 0f, 0f, 0f, 0f, "clean panel");
            Assert.That(title.color, Is.EqualTo(theme.Current.Get(ThemeRole.TextMuted)), "clean: title colour");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright");
            theme.Switch(MakePalette(0.6f));
            Assert.That(card.Banded, Is.True, "bright again");
            AssertOffsets(card.Body, 16f, 16f, -16f, -(48f + 100f + 12f), "bright again: panel");
            Assert.That(title.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.OnPrimary), "bright again: title role");
            AssertTitleOutline(title, theme.Current, "bright again");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f);
            theme.Switch(theme.Current);
            AssertAsBuilt(card, title, ThemeRole.TextMuted, 0f, "clean again");
        }

        // A header without a ThemedGraphic (a colour set by code) is coloured OnPrimary on the band and gets its own colour
        // back, with no component left behind, when the band goes
        [Test]
        public void A_header_without_a_role_gets_its_colour_back()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var sheet = OpenSheet();
            var title = HeaderTitle(sheet.Content, 0f, 100f);
            Object.DestroyImmediate(title.GetComponent<ThemedGraphic>());
            var own = new Color(0.9f, 0.3f, 0.1f, 1f);
            title.color = own;
            sheet.Bind(theme);
            var card = sheet.Panel.GetComponent<KitCard>();
            Assert.That(card.Banded, Is.True, "banded");
            Assert.That(title.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)), "OnPrimary on the band");

            theme.Style = MakeStyle(28f, SurfaceStyle.Soft, 0.4f);
            theme.Switch(theme.Current);
            Assert.That(card.Banded, Is.False, "clean");
            Assert.That(title.color, Is.EqualTo(own), "clean: its own colour");
            Assert.That(title.GetComponent<ThemedGraphic>(), Is.Null, "clean: no role added");
            Assert.That(KitTextOutline.Has(title), Is.False, "clean: no outline");
        }

        // A skin that dresses the kit is never chunky: an outlined chunky label gets its font's own material back
        [Test]
        public void A_skin_that_dresses_the_kit_takes_the_label_outline_off()
        {
            var theme = Theme(MakeStyle(28f, SurfaceStyle.Soft, 0.4f, family: "bright"));
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            button.Bind(theme);
            Assert.That(KitTextOutline.Has(button.Label), Is.True, "chunky: outlined");

            var skin = Skin();
            Assert.That(skin.DressesKit, Is.True, "the skin dresses the kit");
            theme.Switch(theme.Current);

            Assert.That(KitTextOutline.Has(button.Label), Is.False, "skinned: no outline");
            Assert.That(button.Label.fontSharedMaterial, Is.SameAs(button.Label.font.material), "skinned: the font's own material");
        }

        [Test]
        public void Display_font_comes_from_skin()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Core/Presentation/Fonts/Inter/Inter-Regular.ttf");
            Assert.That(source, Is.Not.Null);
            var skin = Skin(source);
            var style = MakeStyle(20f, SurfaceStyle.Soft);

            var display = style.FontFor(TextRole.Display);
            Assert.That(display, Is.Not.Null);
            made.Add(display);
            Assert.That(display, Is.SameAs(skin.DisplayFont));
            Assert.That(style.FontFor(TextRole.Title), Is.SameAs(display));
            Assert.That(style.FontFor(TextRole.Body), Is.SameAs(style.BodyFont), "body text keeps the family's font");
            Assert.That(display.atlasPopulationMode, Is.EqualTo(TMPro.AtlasPopulationMode.Dynamic));
            var fallback = ThemeStyle.Load().DisplayFont;
            Assert.That(display.fallbackFontAssetTable, Is.EqualTo(new List<TMPro.TMP_FontAsset> { fallback }), "falls back to the theme's display font");
        }

        // A currency without an icon file: a disc in its own colour (with a darker rim) and its initial in black or white,
        // whichever reads; with a glyph, the glyph in that colour; null: the plain disc (the HUD without an economy)
        [Test]
        public void Fallback_icon_uses_the_currency_colour_and_initial()
        {
            var root = new GameObject("Icon", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Image Layer(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                return go.GetComponent<Image>();
            }
            var glyph = Layer("Glyph");
            var disc = Layer("Disc");
            var letterGo = new GameObject("Letter", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            letterGo.transform.SetParent(root.transform, false);
            var letter = letterGo.AddComponent<Kturker.UnityBase.Core.Localization.LocalizedText>();
            var icon = root.AddComponent<FallbackIcon>();
            var wiring = new SerializedObject(icon);
            wiring.FindProperty("glyph").objectReferenceValue = glyph;
            wiring.FindProperty("disc").objectReferenceValue = disc;
            wiring.FindProperty("letter").objectReferenceValue = letter;
            wiring.ApplyModifiedPropertiesWithoutUndo();

            var purple = new Color(0.56f, 0.27f, 0.68f);
            icon.ShowCurrency(new CurrencyMark(null, purple, FallbackIcon.LetterOf("sparks")));
            Assert.That(icon.UsesFallback, Is.True);
            Assert.That((glyph.enabled, disc.gameObject.activeSelf, letterGo.activeSelf), Is.EqualTo((false, true, true)));
            Assert.That(disc.color, Is.EqualTo(purple), "the disc in the currency's colour");
            Assert.That(disc.GetComponent<Outline>().effectColor, Is.EqualTo(KitGlyph.Rim(purple)), "with a darker rim");
            Assert.That(letterGo.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("S"));
            Assert.That(letterGo.GetComponent<TMPro.TMP_Text>().color, Is.EqualTo(Color.white), "white on a dark colour");
            Assert.That(new CurrencyMark(null, new Color(1f, 0.85f, 0.3f), "G").LetterColour.grayscale, Is.LessThan(0.2f), "dark on a light one");

            var texture = new Texture2D(4, 4);
            made.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            made.Add(sprite);
            icon.ShowCurrency(new CurrencyMark(sprite, purple, "S"));
            Assert.That(icon.UsesFallback, Is.False);
            Assert.That((glyph.enabled, glyph.sprite, disc.gameObject.activeSelf, letterGo.activeSelf), Is.EqualTo((true, sprite, false, false)));
            Assert.That(glyph.color, Is.EqualTo(purple), "the glyph in the currency's colour");

            icon.ShowCurrency(null);
            Assert.That((glyph.enabled, disc.gameObject.activeSelf, letterGo.activeSelf), Is.EqualTo((false, true, false)), "the plain disc");
            Assert.That(disc.GetComponent<Outline>().enabled, Is.False, "back in its role, no rim");

            Assert.That(FallbackIcon.LetterOf("élan"), Is.EqualTo("É"));
            Assert.That(FallbackIcon.LetterOf("  "), Is.EqualTo("?"));
        }
    }
}
