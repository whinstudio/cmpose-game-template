using System;
using System.Collections.Generic;
using System.Reflection;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The kit's screen parts: the style's background, rounded plain boxes, pills that stay round and how views open
    // and close their panels (KitScreens)
    public sealed class KitScreenTests
    {
        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, 1920f);
            made.Add(canvas);
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private FakeThemeSource Theme(BackgroundStyle background, float radius = 20f)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole))) palette.Set(role, Color.gray);
            palette.Set(ThemeRole.Background, new Color(0.9f, 0.9f, 0.9f, 1f));
            palette.Set(ThemeRole.Primary, new Color(0.1f, 0.3f, 0.9f, 1f));
            palette.Set(ThemeRole.Accent, new Color(0.95f, 0.7f, 0.2f, 1f));
            var style = ScriptableObject.CreateInstance<ThemeStyle>();
            var serialized = new SerializedObject(style);
            serialized.FindProperty("cornerRadius").floatValue = radius;
            serialized.FindProperty("background").enumValueIndex = (int)background;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            made.Add(palette);
            made.Add(style);
            return new FakeThemeSource { Current = palette, Style = style };
        }

        private GameObject Node(string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = size;
            return go;
        }

        private KitBackground Background(FakeThemeSource theme)
        {
            var go = Node("Background", new Vector2(1080f, 1920f));
            go.AddComponent<KitBackdrop>();
            go.AddComponent<ThemedGraphic>().SetRole(ThemeRole.Background);
            var background = go.AddComponent<KitBackground>();
            background.Bind(theme);
            return background;
        }

        // The backdrop's mesh as the canvas would build it
        private static List<UIVertex> Mesh(Graphic graphic)
        {
            var populate = typeof(Graphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(VertexHelper) }, null);
            using (var helper = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { helper });
                var vertices = new List<UIVertex>();
                helper.GetUIVertexStream(vertices);
                return vertices;
            }
        }

        [Test]
        public void A_solid_background_is_one_quad_of_the_background_colour()
        {
            var theme = Theme(BackgroundStyle.Solid);
            var background = Background(theme);
            var mesh = Mesh(background.Backdrop);

            Assert.That(mesh.Count, Is.EqualTo(6), "two triangles");
            foreach (var vertex in mesh) Assert.That((Color)vertex.color, Is.EqualTo((Color)(Color32)theme.Current.Get(ThemeRole.Background)));
        }

        // A graphic draws through the CanvasRenderer on its object; uGUI's own graphics require one each
        [Test]
        public void A_backdrop_brings_its_canvas_renderer()
        {
            Assert.That(Node("Backdrop", Vector2.one).AddComponent<KitBackdrop>().GetComponent<CanvasRenderer>(), Is.Not.Null);
        }

        [Test]
        public void A_gradient_runs_from_a_primary_tint_at_the_top_to_an_accent_tint_at_the_bottom()
        {
            var theme = Theme(BackgroundStyle.Gradient);
            var backdrop = Background(theme).Backdrop;
            var bg = theme.Current.Get(ThemeRole.Background);

            Assert.That(backdrop.Top, Is.EqualTo(Color.Lerp(bg, theme.Current.Get(ThemeRole.Primary), KitBackground.TopTint)));
            Assert.That(backdrop.Bottom, Is.EqualTo(Color.Lerp(bg, theme.Current.Get(ThemeRole.Accent), KitBackground.BottomTint)));
            var mesh = Mesh(backdrop);
            Assert.That(mesh.Count, Is.EqualTo(6), "no pattern");
            foreach (var vertex in mesh)
            {
                var expected = vertex.position.y > 0f ? backdrop.Top : backdrop.Bottom;
                Assert.That((Color)vertex.color, Is.EqualTo((Color)(Color32)expected), $"vertex at {vertex.position}");
            }
        }

        // uGUI draws only the triangles that face the camera on some pipelines: the backdrop winds as its own quads do
        [Test]
        public void The_backdrop_winds_its_triangles_clockwise()
        {
            var mesh = Mesh(Background(Theme(BackgroundStyle.Pattern)).Backdrop);
            for (var i = 0; i < mesh.Count; i += 3)
            {
                var a = mesh[i].position;
                var b = mesh[i + 1].position;
                var c = mesh[i + 2].position;
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.That(cross, Is.LessThan(0f), $"triangle {i / 3}");
            }
        }

        [Test]
        public void A_pattern_adds_faint_primary_diamonds_over_the_gradient()
        {
            var theme = Theme(BackgroundStyle.Pattern);
            var backdrop = Background(theme).Backdrop;

            Assert.That(backdrop.Pattern.a, Is.EqualTo(KitBackground.PatternOpacity).Within(0.001f));
            var mesh = Mesh(backdrop);
            Assert.That(mesh.Count, Is.GreaterThan(6 * 50), "the diamonds cover the screen");
        }

        [Test]
        public void The_background_repaints_when_the_palette_changes()
        {
            var theme = Theme(BackgroundStyle.Solid);
            var backdrop = Background(theme).Backdrop;
            var dark = Object.Instantiate(theme.Current);
            made.Add(dark);
            dark.Set(ThemeRole.Background, Color.black);
            theme.Switch(dark);

            Assert.That(backdrop.Bottom, Is.EqualTo(Color.black));
        }

        [Test]
        public void A_rounded_box_follows_the_style_radius_times_its_scale()
        {
            var image = Node("Box", new Vector2(600f, 300f)).AddComponent<Image>();
            var rounded = image.gameObject.AddComponent<KitRounded>();
            rounded.SetScale(1.25f);
            rounded.Bind(Theme(BackgroundStyle.Solid, 24f));
            Assert.That(KitShape.RadiusOf(image), Is.EqualTo(30f).Within(0.01f));

            rounded.Bind(Theme(BackgroundStyle.Solid, 0f));
            Assert.That(image.sprite, Is.Null, "square style: no sprite");
        }

        // Unity shrinks oversized slice borders per axis, so a long thin pill needs half its short side as its radius
        [Test]
        public void A_pill_keeps_round_ends_as_its_box_changes()
        {
            var image = Node("Rail", new Vector2(800f, 20f)).AddComponent<Image>();
            KitShape.Round(image, KitSprites.Pill);
            Assert.That(KitShape.RadiusOf(image), Is.EqualTo(KitSprites.Pill));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(KitSprites.RoundedRadius / 10f).Within(0.001f));

            image.rectTransform.sizeDelta = new Vector2(800f, 40f);
            image.GetComponent<KitPill>().Fit();
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(KitSprites.RoundedRadius / 20f).Within(0.001f));

            KitShape.Round(image, 12f);
            Assert.That(image.GetComponent<KitPill>().enabled, Is.False, "a plain radius turns the pill off");
            Assert.That(KitShape.RadiusOf(image), Is.EqualTo(12f).Within(0.01f));
        }

        [Test]
        public void Screens_open_sheets_and_transitions_and_close_them_at_once()
        {
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            var full = Node("Full", new Vector2(1080f, 1920f));
            full.AddComponent<CanvasGroup>();
            var transition = full.AddComponent<ScreenTransition>();
            var plain = Node("Plain", Vector2.one);

            KitScreens.Show(sheet.gameObject);
            KitScreens.Show(full);
            KitScreens.Hide(plain);
            Assert.That(sheet.gameObject.activeSelf && sheet.IsOpen, Is.True, "sheet open");
            Assert.That(full.activeSelf && transition.IsShown, Is.True, "screen shown");
            Assert.That(plain.activeSelf, Is.False, "plain hidden");

            KitScreens.Hide(sheet.gameObject);
            KitScreens.Hide(full);
            KitScreens.Set(plain, true);
            Assert.That(sheet.gameObject.activeSelf || sheet.IsOpen, Is.False, "sheet closed at once");
            Assert.That(full.activeSelf || transition.IsShown, Is.False, "screen hidden at once");
            Assert.That(plain.activeSelf, Is.True, "plain shown");
        }

        // Hiding a closed panel (every view does it in Awake) leaves it closed and raises no event
        [Test]
        public void Hiding_a_closed_sheet_is_harmless()
        {
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            var closed = 0;
            sheet.Closed += () => closed++;
            KitScreens.Hide(sheet.gameObject);
            KitScreens.Hide(sheet.gameObject);

            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That(closed, Is.Zero);
        }
    }
}
