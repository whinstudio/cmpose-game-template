using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // WCAG contrast ratio of two opaque palette colours (sRGB values as the palette stores them): 1 = the same
    // luminance, 21 = black on white.
    // - Boxes stand out from what is behind them by BoxOnPanel: Track (the shop's rows on the screen's background)
    //   from Surface; SurfaceVariant (the chosen row, the GitHub card on the Game screen) from Surface and Background.
    // - The chosen row stands out from the other rows by SelectedOnRow (it also shows a Primary side bar).
    // - Secondary text (TextMuted: the per-month detail) reaches SmallText on both row colours; accent text (Primary:
    //   the savings badge) reaches the palette's accent floor there.
    // - Primary parts (slider fill, checkbox mark) on Control, and Primary on Track, reach NonText (WCAG's 3:1 for
    //   user-interface components); Control on Surface reaches the palette's control floor.
    // The floors that differ: on a near-black Surface, Primary text cannot reach 3:1 on two row colours that are 1.10
    // apart while Primary also reaches 3:1 on Track, so dark asks 2.3 (it was 1.88 before); on a white Surface, a
    // Control dark enough for 3:1 against it would leave Primary under 3:1 on it, so light asks 1.4 (dark gets 3).
    public static class Contrast
    {
        public const float BoxOnPanel = 1.25f;
        public const float SelectedOnRow = 1.10f;
        public const float NonText = 3f;
        public const float SmallText = 3f;
        public const float BodyText = 4.5f;

        public static float Ratio(Color a, Color b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Channel(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        private static float Luminance(Color c) => 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);

        public static void AssertBoxes(Palette palette, string name, float accentOnRow, float controlOnSurface)
        {
            var surface = palette.Get(ThemeRole.Surface);
            var primary = palette.Get(ThemeRole.Primary);
            var track = palette.Get(ThemeRole.Track);
            var variant = palette.Get(ThemeRole.SurfaceVariant);
            var control = palette.Get(ThemeRole.Control);
            Assert.That(Ratio(track, surface), Is.GreaterThanOrEqualTo(BoxOnPanel), $"{name}: track on surface");
            Assert.That(Ratio(variant, surface), Is.GreaterThanOrEqualTo(BoxOnPanel), $"{name}: surface variant on surface");
            Assert.That(Ratio(variant, palette.Get(ThemeRole.Background)), Is.GreaterThanOrEqualTo(BoxOnPanel), $"{name}: surface variant on background");
            Assert.That(Ratio(variant, track), Is.GreaterThanOrEqualTo(SelectedOnRow), $"{name}: chosen row on the others");
            foreach (var (row, rowName) in new[] { (track, "track"), (variant, "surface variant") })
            {
                Assert.That(Ratio(palette.Get(ThemeRole.TextMuted), row), Is.GreaterThanOrEqualTo(SmallText), $"{name}: muted text on {rowName}");
                Assert.That(Ratio(primary, row), Is.GreaterThanOrEqualTo(accentOnRow), $"{name}: primary text on {rowName}");
            }
            Assert.That(Ratio(primary, track), Is.GreaterThanOrEqualTo(NonText), $"{name}: primary on track");
            Assert.That(Ratio(primary, control), Is.GreaterThanOrEqualTo(NonText), $"{name}: primary on control");
            Assert.That(Ratio(control, surface), Is.GreaterThanOrEqualTo(controlOnSurface), $"{name}: control on surface");
        }

        // The roles a game adds to the shell's: Accent (a second call to action, OnAccent text on it), Success, Danger
        public static readonly ThemeRole[] AccentAndStatus = { ThemeRole.Accent, ThemeRole.OnAccent, ThemeRole.Success, ThemeRole.Danger };

        public static void AssertAccentAndStatus(Palette palette, string name)
        {
            foreach (var role in AccentAndStatus)
                Assert.That(palette.Get(role).a, Is.EqualTo(1f), $"{name}: {role} is opaque");
            Assert.That(palette.Get(ThemeRole.Accent), Is.Not.EqualTo(palette.Get(ThemeRole.Primary)), $"{name}: accent is not primary");
            Assert.That(palette.Get(ThemeRole.Success), Is.Not.EqualTo(palette.Get(ThemeRole.Danger)), $"{name}: success is not danger");
        }

        public static void AssertAccentReads(Palette palette, string name) =>
            Assert.That(Ratio(palette.Get(ThemeRole.OnAccent), palette.Get(ThemeRole.Accent)), Is.GreaterThanOrEqualTo(BodyText), $"{name}: text on accent");

        public static void AssertDangerReads(Palette palette, string name) =>
            Assert.That(Ratio(palette.Get(ThemeRole.Danger), palette.Get(ThemeRole.Surface)), Is.GreaterThanOrEqualTo(NonText), $"{name}: danger on surface");
    }

    public sealed class ThemeTests
    {
        private static Palette Make(Color seed)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                palette.Set(role, new Color(seed.r, seed.g, (int)role / 10f, 1f));
            return palette;
        }

        [Test]
        public void A_palette_keeps_a_colour_per_role()
        {
            var palette = Make(Color.red);
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                Assert.That(palette.Get(role), Is.EqualTo(new Color(1f, 0f, (int)role / 10f, 1f)), role.ToString());
            Object.DestroyImmediate(palette);
        }

        [Test]
        public void The_light_palette_sets_boxes_apart_and_primary_on_track()
        {
            var light = AssetDatabase.LoadAssetAtPath<Palette>("Assets/_Project/Core/Presentation/Resources/Theme/Light.asset");
            Assert.That(light, Is.Not.Null);
            Contrast.AssertBoxes(light, "light", accentOnRow: 3f, controlOnSurface: 1.4f);
        }

        private static Palette LightAsset() =>
            AssetDatabase.LoadAssetAtPath<Palette>("Assets/_Project/Core/Presentation/Resources/Theme/Light.asset");

        [Test]
        public void New_roles_have_colours_in_the_light_palette()
        {
            Contrast.AssertAccentAndStatus(LightAsset(), "light");
        }

        [Test]
        public void Accent_reads_on_accent_in_the_light_palette()
        {
            Contrast.AssertAccentReads(LightAsset(), "light");
        }

        [Test]
        public void Danger_reads_on_surface_in_the_light_palette()
        {
            Contrast.AssertDangerReads(LightAsset(), "light");
        }

        [Test]
        public void The_style_asset_loads_from_resources()
        {
            var style = ThemeStyle.Load();
            // Generated projects carry the family the generator applied, not only the template's default
            Assert.That(new[] { "bright", "neon", "pastel", "bold", "nature", "clean" }, Does.Contain(style.FamilyId));
            Assert.That(style.DisplayFont, Is.Not.Null);
            Assert.That(style.BodyFont, Is.Not.Null);
            Assert.That(style.CornerRadius, Is.InRange(0f, 48f));
            Assert.That(style.ShadowStrength, Is.InRange(0f, 1f));
            Assert.That(style.WorldColors.Count, Is.InRange(3, 4));
        }

        [Test]
        public void Themed_text_uses_the_display_font_for_titles()
        {
            var display = TMP_Settings.defaultFontAsset;
            // A second font of its own (a copy of the default would share its atlas, an asset that must not be destroyed)
            var body = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(display.creationSettings.sourceFontFileGUID)));
            var style = ScriptableObject.CreateInstance<ThemeStyle>();
            var serialized = new SerializedObject(style);
            serialized.FindProperty("displayFont").objectReferenceValue = display;
            serialized.FindProperty("bodyFont").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var source = new FakeThemeSource { Current = Make(Color.white), Style = style };
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var binder = new UiBinder(new TableLocalizer(new FakeStringSource(), new FakeLog(), false), source);
                foreach (TextRole role in Enum.GetValues(typeof(TextRole)))
                {
                    var go = new GameObject(role.ToString(), typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<TextMeshProUGUI>();
                    var themed = go.AddComponent<ThemedText>();
                    var field = new SerializedObject(themed);
                    field.FindProperty("role").enumValueIndex = (int)role;
                    field.ApplyModifiedPropertiesWithoutUndo();
                }
                binder.BindTree(root);
                Assert.That(root.transform.Find("Display").GetComponent<TMP_Text>().font, Is.SameAs(display));
                Assert.That(root.transform.Find("Title").GetComponent<TMP_Text>().font, Is.SameAs(display));
                Assert.That(root.transform.Find("Body").GetComponent<TMP_Text>().font, Is.SameAs(body));
                Assert.That(root.transform.Find("Caption").GetComponent<TMP_Text>().font, Is.SameAs(body));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source.Current);
                Object.DestroyImmediate(style);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void Themed_text_keeps_its_font_when_the_style_has_none()
        {
            var style = ScriptableObject.CreateInstance<ThemeStyle>();
            var source = new FakeThemeSource { Style = style };
            var go = new GameObject("Title", typeof(RectTransform));
            try
            {
                var text = go.AddComponent<TextMeshProUGUI>();
                var before = text.font;
                go.AddComponent<ThemedText>().Bind(source);
                Assert.That(text.font, Is.SameAs(before));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void Themed_graphics_and_cameras_follow_the_source()
        {
            var light = Make(Color.white);
            var dark = Make(Color.black);
            var source = new FakeThemeSource { Current = light };
            var go = new GameObject("Button", typeof(RectTransform));
            var cameraGo = new GameObject("Camera", typeof(Camera));
            try
            {
                var image = go.AddComponent<Image>();
                var themed = go.AddComponent<ThemedGraphic>();
                var camera = cameraGo.GetComponent<Camera>();
                var themedCamera = cameraGo.AddComponent<ThemedCamera>();

                themed.Bind(source);
                themedCamera.Bind(source);
                Assert.That(themed.Role, Is.EqualTo(ThemeRole.Background));
                Assert.That(image.color, Is.EqualTo(light.Get(ThemeRole.Background)));
                Assert.That(camera.backgroundColor, Is.EqualTo(light.Get(ThemeRole.Background)));

                source.Switch(dark);
                Assert.That(image.color, Is.EqualTo(dark.Get(ThemeRole.Background)));
                Assert.That(camera.backgroundColor, Is.EqualTo(dark.Get(ThemeRole.Background)));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(light);
                Object.DestroyImmediate(dark);
            }
        }
    }
}
