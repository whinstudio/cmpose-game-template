using System;
using System.Collections.Generic;
using System.Linq;
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
    // The kit's layout rules: a round icon in the middle of its button (a dressed one on the sprite's visible face), a
    // touch area of at least KitLayout.MinTouch, a row of items KitLayout.Gap apart that shrinks before it wraps, and a
    // modal card within a share of its parent's width and height, scaled down to fit.
    public sealed class KitLayoutTests
    {
        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, 1920f);
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
        }

        private FakeThemeSource Theme()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                palette.Set(role, new Color(0.2f, (int)role / 20f, 0.8f, 1f));
            made.Add(palette);
            return new FakeThemeSource { Current = palette };
        }

        // A sliced 48 x height sprite at 100 px per unit, border (left, bottom, right, top) 12 px unless given
        private Sprite Sprite(string name, int height, Vector4? border = null)
        {
            var texture = new Texture2D(48, height) { name = name };
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, 48f, height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border ?? new Vector4(12f, 12f, 12f, 12f));
            sprite.name = name;
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        private static Vector2 Centre(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) / 2f;
        }

        // Left, bottom, right, top in world units (the canvas is unscaled: canvas units)
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // The scale a card height units high takes on a landscape parent parentHeight units high: the parent's height less
        // the default margin above and below, and at most KitFitScale.LandscapeHeight of it
        private static float LandscapeFit(float parentHeight, float height) =>
            Mathf.Min((parentHeight - 2f * KitLayout.Gap) / height, KitFitScale.LandscapeHeight * parentHeight / height);

        private RectTransform Box(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            return rect;
        }

        // A row width units wide holding count items 200 x 100 (preferred), laid out
        private List<RectTransform> Row(float width, int count, out KitFlowRow row)
        {
            var rect = Box("Row", canvas.transform, new Vector2(width, 400f));
            row = rect.gameObject.AddComponent<KitFlowRow>();
            var items = new List<RectTransform>();
            for (var i = 0; i < count; i++)
            {
                var item = Box($"Item{i}", rect, Vector2.zero);
                var element = item.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 200f;
                element.preferredHeight = 100f;
                items.Add(item);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return items;
        }

        [Test]
        public void Round_icon_sits_in_the_middle()
        {
            var button = KitFactory.Button(canvas.transform, "Settings", KitButtonVariant.Icon, KitSprites.Gear);
            button.Bind(Theme());

            Assert.That(button.ContentOffset, Is.EqualTo(Vector2.zero));
            var icon = Centre(button.Icon.rectTransform);
            var body = Centre(button.Body.rectTransform);
            Assert.That(icon.x, Is.EqualTo(body.x).Within(0.5f));
            Assert.That(icon.y, Is.EqualTo(body.y).Within(0.5f));
        }

        [Test]
        public void Dressed_round_icon_sits_on_the_visual_middle()
        {
            // The icon entry's normal sprite stands 4 px taller than the pressed one: its face is 4 px above the base
            var sprites = new Dictionary<string, Sprite>
            {
                ["Art/pixel-ui-pack/red"] = Sprite("red", 52),
                ["Art/pixel-ui-pack/red_pressed"] = Sprite("red_pressed", 48)
            };
            var skin = GameSkin.Parse(SkinSprites.Json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = SkinSprites.Make(made, path.Substring(path.LastIndexOf('/') + 1));
                return sprite;
            }, _ => null);
            GameSkin.ForTests(skin);
            Assert.That(skin.Button(KitButtonVariant.Icon).Depth, Is.EqualTo(4f));
            Assert.That(skin.Button(KitButtonVariant.Primary).Depth, Is.EqualTo(0f), "equal sprites have no depth");

            var button = KitFactory.Button(canvas.transform, "Settings", KitButtonVariant.Icon, KitSprites.Gear);
            button.Bind(Theme());

            // 4 px at sliceScale 4 is 16 units, half of it 8
            Assert.That(button.ContentOffset.y, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Centre(button.Icon.rectTransform).y - Centre(button.Body.rectTransform).y, Is.EqualTo(8f).Within(0.5f));
            // The label also sits its font's cap offset off: only the face's lift is measured
            Assert.That(Centre(button.Label.rectTransform).y - Centre(button.Body.rectTransform).y - KitButton.CapOffset(button.Label),
                Is.EqualTo(8f).Within(0.5f));

            button.OnPointerDown(new PointerEventData(null));
            Assert.That(button.ContentOffset, Is.EqualTo(Vector2.zero), "pressed: the face is down");
            Assert.That(Centre(button.Icon.rectTransform).y, Is.EqualTo(Centre(button.Body.rectTransform).y).Within(0.5f));

            button.OnPointerUp(new PointerEventData(null));
            Assert.That(button.ContentOffset.y, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Centre(button.Icon.rectTransform).y - Centre(button.Body.rectTransform).y, Is.EqualTo(8f).Within(0.5f));

            // Undressed: back in the middle, not 8 units off
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            button.Bind(Theme());
            Assert.That(button.ContentOffset, Is.EqualTo(Vector2.zero));
            Assert.That(Centre(button.Icon.rectTransform).y, Is.EqualTo(Centre(button.Body.rectTransform).y).Within(0.5f));
        }

        [Test]
        public void Equal_size_skin_lifts_label_by_half_the_border_difference()
        {
            // A Kenney pack: normal and pressed are both 48 x 48, but the normal sprite's shadow lip makes its bottom
            // border 2 px thicker than its top, so the middle of its nine-slice centre is 1 px above the sprite's
            var sprites = new Dictionary<string, Sprite>
            {
                ["Art/pixel-ui-pack/blue"] = SkinSprites.Make(made, "blue", new Vector4(3f, 5f, 3f, 3f)),
                ["Art/pixel-ui-pack/blue_pressed"] = SkinSprites.Make(made, "blue_pressed", new Vector4(2f, 2f, 2f, 2f))
            };
            var skin = GameSkin.Parse(SkinSprites.Json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = SkinSprites.Make(made, path.Substring(path.LastIndexOf('/') + 1));
                return sprite;
            }, _ => null);
            GameSkin.ForTests(skin);
            Assert.That(skin.Button(KitButtonVariant.Primary).Depth, Is.EqualTo(0f), "equal sprites have no depth");

            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(Theme());

            // 1 px at sliceScale 4 is 4 units
            Assert.That(button.ContentOffset.y, Is.EqualTo(4f).Within(0.001f));
            // The label also sits its font's cap offset off: only the face's lift is measured
            Assert.That(Centre(button.Label.rectTransform).y - Centre(button.Body.rectTransform).y - KitButton.CapOffset(button.Label),
                Is.EqualTo(4f).Within(0.5f));

            // The pressed sprite's borders are even: its centre's middle is the sprite's
            button.OnPointerDown(new PointerEventData(null));
            Assert.That(button.ContentOffset, Is.EqualTo(Vector2.zero), "pressed: even borders");

            button.OnPointerUp(new PointerEventData(null));
            Assert.That(button.ContentOffset.y, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void Taller_skin_with_a_lip_lifts_label_by_the_larger_of_the_two()
        {
            // The normal sprite stands 8 px taller (Depth 8) and its bottom border is 4 px thicker than its top (lip 4):
            // the face is lifted by the larger, not by both
            var sprites = new Dictionary<string, Sprite>
            {
                ["Art/pixel-ui-pack/blue"] = Sprite("blue", 56, new Vector4(3f, 7f, 3f, 3f)),
                ["Art/pixel-ui-pack/blue_pressed"] = SkinSprites.Make(made, "blue_pressed", new Vector4(2f, 2f, 2f, 2f))
            };
            var skin = GameSkin.Parse(SkinSprites.Json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = SkinSprites.Make(made, path.Substring(path.LastIndexOf('/') + 1));
                return sprite;
            }, _ => null);
            GameSkin.ForTests(skin);
            var entry = skin.Button(KitButtonVariant.Primary);
            Assert.That(entry.Depth, Is.EqualTo(8f));
            Assert.That(entry.Lift(entry.Normal), Is.EqualTo(4f).Within(0.001f), "max(8, 4) / 2");

            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(Theme());

            // 4 px at sliceScale 4 is 16 units (the sum would give 24)
            Assert.That(button.ContentOffset.y, Is.EqualTo(16f).Within(0.001f));
        }

        // Family fonts whose capitals TMP's Center leaves well off the middle (KitButton.CapOffset at 56 pt): Exo 2's
        // about 3 units low, Baloo 2's about 1.8 high
        private static readonly string[] OffCentreFonts = { "Exo2/Exo2-Regular SDF", "Baloo2/Baloo2-Bold SDF" };

        // The app's style with font in place of both of its own (a button's label takes the body font, Play's the display
        // font: KitButton.LabelFont)
        private FakeThemeSource Theme(TMP_FontAsset font)
        {
            var style = Object.Instantiate(ThemeStyle.Load());
            made.Add(style);
            var serialized = new SerializedObject(style);
            serialized.FindProperty("displayFont").objectReferenceValue = font;
            serialized.FindProperty("bodyFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var theme = Theme();
            theme.Style = style;
            return theme;
        }

        // The label's capitals (H) in the body's space: the middle of a line's capitals on the face's middle, and a
        // wrapped label's first cap line as far above it as its last baseline is below
        private static void AssertCapitalsCentred(KitButton button, string font)
        {
            var label = button.Label;
            var body = button.Body.rectTransform;
            var middle = body.rect.center.y + button.ContentOffset.y;
            float Y(float y) => body.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(0f, y, 0f))).y;

            // topLeft and bottomLeft include the atlas padding, the same above and below a glyph: it cancels in a
            // middle, and the first and the last line's H carry the same
            label.text = "HH";
            label.ForceMeshUpdate();
            var h = label.textInfo.characterInfo[0];
            Assert.That((Y(h.topLeft.y) + Y(h.bottomLeft.y)) / 2f, Is.EqualTo(middle).Within(1f), $"{font}: one line");

            label.text = "HH HH HH HH HH HH HH HH";
            label.ForceMeshUpdate();
            var info = label.textInfo;
            Assert.That(info.lineCount, Is.GreaterThanOrEqualTo(2), $"{font}: wraps");
            var top = Y(info.characterInfo[info.lineInfo[0].firstCharacterIndex].topLeft.y);
            var bottom = Y(info.characterInfo[info.lineInfo[info.lineCount - 1].firstCharacterIndex].bottomLeft.y);
            Assert.That(top - middle, Is.EqualTo(middle - bottom).Within(1f), $"{font}: two lines");
        }

        [Test]
        public void Capitals_sit_in_the_middle_of_the_face()
        {
            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            FakeThemeSource theme = null;
            foreach (var path in OffCentreFonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/_Project/Core/Presentation/Fonts/{path}.asset");
                Assert.That(font, Is.Not.Null, path);
                theme = Theme(font);
                button.Bind(theme);
                Assert.That(button.Label.font, Is.SameAs(font), path);
                AssertCapitalsCentred(button, path);
            }

            // A game may delete a skin font its skin does not name (MAINTAINING.md, "Skin fonts")
            var pixelify = SkinFonts.Load(SkinFonts.PixelifySans);
            if (pixelify == null) return;
            // The skin's font reaches the label after the button applied (its ThemedText bound alone): the capitals follow
            GameSkin.ForTests(GameSkin.Parse("{\"version\":2,\"fonts\":{\"display\":\"pixelify-sans\",\"body\":\"pixelify-sans\"}}",
                _ => null, _ => null));
            button.Label.GetComponent<ThemedText>().Bind(theme);
            Assert.That(button.Label.font, Is.SameAs(pixelify));
            AssertCapitalsCentred(button, "Pixelify Sans");
        }

        // How far the middle of the label's capitals (its first cap line to its last baseline, H's edges as above)
        // stands above the face's middle, in the body's units, after TMP lays text out
        private static float CapitalsOff(KitButton button, string text)
        {
            var label = button.Label;
            var body = button.Body.rectTransform;
            float Y(float y) => body.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(0f, y, 0f))).y;
            label.text = text;
            label.ForceMeshUpdate();
            var info = label.textInfo;
            var top = Y(info.characterInfo[info.lineInfo[0].firstCharacterIndex].topLeft.y);
            var bottom = Y(info.characterInfo[info.lineInfo[info.lineCount - 1].firstCharacterIndex].bottomLeft.y);
            return (top + bottom) / 2f - (body.rect.center.y + button.ContentOffset.y);
        }

        [Test]
        public void Capitals_stay_centred_when_the_label_shrinks()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Core/Presentation/Fonts/Exo2/Exo2-Regular SDF.asset");
            Assert.That(font, Is.Not.Null);
            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(Theme(font));
            var label = button.Label;
            // Room to shrink well below the largest size, where a correction made for that size is a unit and more off
            label.fontSizeMin = 16f;
            var text = string.Join(" ", Enumerable.Repeat("HHHH", 30));

            Assert.That(CapitalsOff(button, text), Is.EqualTo(0f).Within(1f), $"at {label.fontSize} pt");
            Assert.That(label.textInfo.lineCount, Is.GreaterThan(1), "wrapped");
            Assert.That(label.fontSize, Is.LessThan(label.fontSizeMax * 0.6f), "shrunk");

            // Back to a short label: the largest size, centred again
            Assert.That(CapitalsOff(button, "HH"), Is.EqualTo(0f).Within(1f), $"at {label.fontSize} pt");
            Assert.That(label.fontSize, Is.EqualTo(label.fontSizeMax));

            // Moving the label does not lay it out again (no loop): nothing is left dirty, a canvas update draws nothing new
            var laidOut = 0;
            label.OnPreRenderText += _ => laidOut++;
            label.rectTransform.anchoredPosition += new Vector2(0f, 2f);
            Canvas.ForceUpdateCanvases();
            Assert.That(label.havePropertiesChanged, Is.False);
            Assert.That(laidOut, Is.EqualTo(0));
        }

        private static TMP_FontAsset FamilyFont(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/_Project/Core/Presentation/Fonts/{path}.asset");
            Assert.That(font, Is.Not.Null, path);
            return font;
        }

        // A display text sized 60, autosized or not between 30 and 72
        private TMP_Text DisplayText(string name, bool autoSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 60f;
            text.enableAutoSizing = autoSize;
            text.fontSizeMin = 30f;
            text.fontSizeMax = 72f;
            go.AddComponent<ThemedText>().SetRole(TextRole.Display);
            return text;
        }

        // A tall font (Baloo 2: 1.6 em) lets a text shrink to fit down to the size at which its line box is
        // ThemedText.KitLine, from its own sizes; bound again (keeping what code sized since), or a bound copy bound
        // (MenuView's game buttons), it shrinks no further, and a font that is not tall gives the text its own sizing back
        [Test]
        public void A_tall_font_lets_a_text_shrink_to_the_kit_line_box()
        {
            var baloo = FamilyFont("Baloo2/Baloo2-Bold SDF");
            var nunito = FamilyFont("Nunito/Nunito-Bold SDF");
            Assert.That(ThemedText.LineBox(baloo), Is.EqualTo(1.6f).Within(0.01f));
            Assert.That(ThemedText.IsTall(baloo), Is.True);
            Assert.That(ThemedText.IsTall(nunito), Is.False, "Nunito's 1.36 em, the tallest of the others");
            var share = ThemedText.KitLine / ThemedText.LineBox(baloo);
            var fixedSize = DisplayText("Fixed", false);
            var autoSized = DisplayText("Auto", true);
            var tall = Theme(baloo);

            fixedSize.GetComponent<ThemedText>().Bind(tall);
            autoSized.GetComponent<ThemedText>().Bind(tall);
            Assert.That(fixedSize.font, Is.SameAs(baloo));
            Assert.That(fixedSize.enableAutoSizing, Is.True, "a fixed size may shrink");
            Assert.That(fixedSize.fontSizeMax, Is.EqualTo(60f), "never above its own size");
            Assert.That(fixedSize.fontSizeMin, Is.EqualTo(60f * share).Within(0.001f));
            Assert.That(autoSized.fontSizeMax, Is.EqualTo(72f));
            Assert.That(autoSized.fontSizeMin, Is.EqualTo(30f * share).Within(0.001f), "below its own minimum by the share");

            fixedSize.GetComponent<ThemedText>().Bind(tall);
            Assert.That(fixedSize.fontSizeMin, Is.EqualTo(60f * share).Within(0.001f), "bound twice");
            // Code sized it after binding: the next scene's binding keeps that
            autoSized.fontSizeMax = 80f;
            autoSized.GetComponent<ThemedText>().Bind(tall);
            Assert.That(autoSized.fontSizeMax, Is.EqualTo(80f), "sized by code, bound again");
            var copy = Object.Instantiate(fixedSize.gameObject, canvas.transform, false).GetComponent<TMP_Text>();
            copy.GetComponent<ThemedText>().Bind(tall);
            Assert.That(copy.fontSizeMax, Is.EqualTo(60f), "a bound copy");
            Assert.That(copy.fontSizeMin, Is.EqualTo(60f * share).Within(0.001f), "a bound copy");

            var normal = Theme(nunito);
            foreach (var text in new[] { fixedSize, copy, autoSized }) text.GetComponent<ThemedText>().Bind(normal);
            foreach (var text in new[] { fixedSize, copy })
            {
                Assert.That(text.enableAutoSizing, Is.False, text.name);
                Assert.That(text.fontSize, Is.EqualTo(60f), text.name);
            }
            Assert.That(autoSized.enableAutoSizing, Is.True);
            foreach (var text in new[] { fixedSize, copy, autoSized })
            {
                Assert.That(text.font, Is.SameAs(nunito), text.name);
                Assert.That(text.fontSizeMin, Is.EqualTo(30f), text.name);
                Assert.That(text.fontSizeMax, Is.EqualTo(72f), text.name);
            }
        }

        [Test]
        public void Touch_area_reaches_the_minimum()
        {
            var toggle = KitFactory.Switch(canvas.transform, "Switch");
            var hit = KitLayout.AddTouchArea(toggle.GetComponent<Toggle>());
            var hitRect = (RectTransform)hit.transform;

            Assert.That(hit.name, Is.EqualTo("Hit"));
            Assert.That(hit.transform.GetSiblingIndex(), Is.EqualTo(0), "drawn first, under the parts");
            var image = hit.GetComponent<Image>();
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(image.color.a, Is.EqualTo(0f), "invisible");
            Assert.That(hitRect.rect.size.x, Is.EqualTo(132f).Within(0.01f));
            Assert.That(hitRect.rect.size.y, Is.EqualTo(KitLayout.MinTouch).Within(0.01f));
            var centre = Centre(hitRect);
            var parent = Centre((RectTransform)toggle.transform);
            Assert.That(centre.x, Is.EqualTo(parent.x).Within(0.01f));
            Assert.That(centre.y, Is.EqualTo(parent.y).Within(0.01f));

            // It follows the parent's size
            ((RectTransform)toggle.transform).sizeDelta = new Vector2(100f, 140f);
            Assert.That(hitRect.rect.size.x, Is.EqualTo(KitLayout.MinTouch).Within(0.01f));
            Assert.That(hitRect.rect.size.y, Is.EqualTo(140f).Within(0.01f));

            var button = Box("Button", canvas.transform, new Vector2(200f, 200f)).gameObject.AddComponent<Button>();
            var big = (RectTransform)KitLayout.AddTouchArea(button).transform;
            Assert.That(big.rect.size.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(big.rect.size.y, Is.EqualTo(200f).Within(0.01f));

            // A theme keeps it invisible
            hit.GetComponent<ThemedGraphic>().Bind(Theme());
            Assert.That(image.color.a, Is.EqualTo(0f), "bound: still invisible");
        }

        [Test]
        public void Flow_row_keeps_a_gap()
        {
            var items = Row(800f, 3, out _);

            for (var i = 0; i < items.Count; i++)
                Assert.That(items[i].localScale.x, Is.EqualTo(1f).Within(0.0001f), $"item {i} scale");
            for (var i = 1; i < items.Count; i++)
            {
                Assert.That(Bounds(items[i]).xMin - Bounds(items[i - 1]).xMax, Is.EqualTo(KitLayout.Gap).Within(0.01f), $"gap before {i}");
                Assert.That(Bounds(items[i]).yMin, Is.EqualTo(Bounds(items[0]).yMin).Within(0.01f), $"item {i} on the first line");
            }
        }

        [Test]
        public void Flow_row_shrinks_before_it_wraps()
        {
            var items = Row(700f, 4, out _);
            var scale = (700f - 3f * KitLayout.Gap) / 800f;

            for (var i = 0; i < items.Count; i++)
            {
                Assert.That(items[i].localScale.x, Is.EqualTo(scale).Within(0.0001f), $"item {i} scale");
                Assert.That(Bounds(items[i]).yMin, Is.EqualTo(Bounds(items[0]).yMin).Within(0.01f), $"item {i} on one line");
            }
            for (var i = 1; i < items.Count; i++)
                Assert.That(Bounds(items[i]).xMin - Bounds(items[i - 1]).xMax, Is.EqualTo(KitLayout.Gap).Within(0.01f), $"gap before {i}");
            var row = Bounds((RectTransform)items[0].parent);
            Assert.That(Bounds(items[items.Count - 1]).xMax, Is.LessThanOrEqualTo(row.xMax + 0.01f), "inside the row");
        }

        [Test]
        public void Flow_row_wraps_below_the_minimum_scale()
        {
            var items = Row(700f, 8, out var flow);
            var row = Bounds((RectTransform)flow.transform);

            var lines = new List<float>();
            foreach (var item in items)
            {
                Assert.That(item.localScale.x, Is.EqualTo(KitFlowRow.MinScale).Within(0.0001f), $"{item.name} scale");
                var bounds = Bounds(item);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(row.xMin - 0.01f), $"{item.name} left");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(row.xMax + 0.01f), $"{item.name} right");
                if (!lines.Exists(top => Mathf.Abs(top - bounds.yMax) < 0.01f)) lines.Add(bounds.yMax);
            }
            Assert.That(lines.Count, Is.EqualTo(2), "two lines");
            var first = Bounds(items[0]);
            var second = Bounds(items[items.Count - 1]);
            Assert.That(first.yMin - second.yMax, Is.EqualTo(KitLayout.Gap).Within(0.01f), "line gap");
            for (var i = 0; i < items.Count; i++)
            for (var j = i + 1; j < items.Count; j++)
                Assert.That(Bounds(items[i]).Overlaps(Bounds(items[j])), Is.False, $"{items[i].name} and {items[j].name} overlap");
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)flow.transform),
                Is.EqualTo(2f * 100f * KitFlowRow.MinScale + KitLayout.Gap).Within(0.01f), "preferred height: both lines");
        }

        // A row's own minimum (the menu's round buttons: never under the touch size) wraps sooner than the kit's default
        [Test]
        public void Flow_row_keeps_its_own_minimum_scale()
        {
            var items = Row(1000f, 5, out var flow);
            Assert.That(flow.Minimum, Is.EqualTo(KitFlowRow.MinScale), "the kit's default");
            Assert.That(items[4].localScale.x, Is.LessThan(1f), "five fit one line scaled down by default");

            flow.SetMinimum(0.97f);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)flow.transform);
            var lines = new List<float>();
            foreach (var item in items)
            {
                Assert.That(item.localScale.x, Is.EqualTo(0.97f).Within(0.0001f), $"{item.name} scale");
                var top = Bounds(item).yMax;
                if (!lines.Exists(t => Mathf.Abs(t - top) < 0.01f)) lines.Add(top);
            }
            Assert.That(lines.Count, Is.EqualTo(2), "wrapped instead of shrinking further");
            Assert.That(() => flow.SetMinimum(0f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => flow.SetMinimum(1.1f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Fit_scale_shrinks_a_tall_card()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1920f, 1080f));
            var card = Box("Card", parent, new Vector2(900f, 1500f));
            var fit = card.gameObject.AddComponent<KitFitScale>();

            Assert.That(fit.Margin, Is.EqualTo(KitLayout.Gap));
            Assert.That(card.localScale.x, Is.EqualTo(LandscapeFit(1080f, 1500f)).Within(0.0001f));
            Assert.That(card.localScale.y, Is.EqualTo(card.localScale.x));

            card.sizeDelta = new Vector2(900f, 900f);
            Assert.That(card.localScale, Is.EqualTo(Vector3.one), "fits: full size");

            // A shorter parent shrinks it again
            parent.sizeDelta = new Vector2(1920f, 600f);
            fit.Fit();
            Assert.That(card.localScale.x, Is.EqualTo(LandscapeFit(600f, 900f)).Within(0.0001f));
        }

        // A card built before its parent is laid out (a prefab built off-screen) keeps its full size, then fits once the
        // parent has a height
        [Test]
        public void Fit_scale_waits_for_a_laid_out_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1920f, 0f));
            var card = Box("Card", parent, new Vector2(900f, 1500f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.localScale, Is.EqualTo(Vector3.one), "no height yet: full size");
            Assert.That(fit.Scale, Is.EqualTo(1f));

            parent.sizeDelta = new Vector2(1920f, 1080f);
            fit.Fit();
            Assert.That(card.localScale.x, Is.EqualTo(LandscapeFit(1080f, 1500f)).Within(0.0001f));
            Assert.That(fit.Scale, Is.LessThan(1f));
        }

        // A fixed-height card grows with a parent taller than the 1920-unit reference, as a band of it would
        [Test]
        public void Fitted_card_grows_on_a_tall_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 2400f));
            var card = Box("Card", parent, new Vector2(900f, 1400f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.height, Is.EqualTo(1400f * 2400f / KitFitScale.ReferenceHeight).Within(0.01f));
            Assert.That(card.localScale, Is.EqualTo(Vector3.one));
            Assert.That(fit.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void Fitted_card_keeps_its_height_on_the_reference_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, KitFitScale.ReferenceHeight));
            var card = Box("Card", parent, new Vector2(900f, 1400f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.height, Is.EqualTo(1400f).Within(0.01f));
            Assert.That(card.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Fitted_card_shrinks_on_a_short_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1920f, 1080f));
            var card = Box("Card", parent, new Vector2(900f, 1400f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.height, Is.EqualTo(1400f).Within(0.01f));
            Assert.That(card.localScale.y, Is.EqualTo(LandscapeFit(1080f, 1400f)).Within(0.0001f));

            // On a tall parent again it grows from its authored height, and back on the reference it returns to it
            parent.sizeDelta = new Vector2(1080f, 2400f);
            fit.Fit();
            Assert.That(card.rect.height, Is.EqualTo(1400f * 2400f / KitFitScale.ReferenceHeight).Within(0.01f), "tall again");
            Assert.That(card.localScale, Is.EqualTo(Vector3.one), "tall again: full size");
            parent.sizeDelta = new Vector2(1080f, KitFitScale.ReferenceHeight);
            fit.Fit();
            Assert.That(card.rect.height, Is.EqualTo(1400f).Within(0.01f), "back on the reference");
        }

        // A bounded card stretched across its parent keeps its margin from the sides where its width share would come
        // closer (a window whose Close X overhangs it), and takes its share where that leaves more
        [Test]
        public void Stretched_card_keeps_its_margin_from_the_sides()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 1920f));
            var card = Box("Card", parent, new Vector2(0f, 1000f));
            card.anchorMin = new Vector2(0f, 0.5f);
            card.anchorMax = new Vector2(1f, 0.5f);
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            Assert.That(card.rect.width, Is.EqualTo(1080f * KitFitScale.PortraitWidth).Within(0.01f), "its share");

            var serialized = new SerializedObject(fit);
            serialized.FindProperty("margin").floatValue = 93f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fit.Fit();
            Assert.That(card.rect.width, Is.EqualTo(1080f - 2f * 93f).Within(0.01f), "its margin");
            parent.sizeDelta = new Vector2(1920f, 1080f);
            fit.Fit();
            Assert.That(card.rect.width, Is.EqualTo(1920f * KitFitScale.LandscapeWidth).Within(0.01f), "its share again");
        }

        // A card as tall as its content (the Settings card) keeps the height its ContentSizeFitter gives it
        [Test]
        public void Content_sized_card_does_not_grow()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 2400f));
            var card = Box("Card", parent, new Vector2(900f, 1400f));
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 900f;
            element.preferredHeight = 1400f;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.height, Is.EqualTo(1400f).Within(0.01f));
            Assert.That(card.localScale, Is.EqualTo(Vector3.one));
        }

        // An aspect-fitted card sizes itself too: KitFitScale leaves its height to the fitter
        [Test]
        public void Aspect_fitted_card_does_not_grow()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 2400f));
            // 1800 tall fits 80 % of 2400 (1920); grown to 2250 it would not, so a grow the fitter takes back would still
            // shrink the card
            var card = Box("Card", parent, new Vector2(900f, 1800f));
            var aspect = card.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            aspect.aspectRatio = 900f / 1800f;
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.height, Is.EqualTo(1800f).Within(0.01f));
            Assert.That(card.localScale, Is.EqualTo(Vector3.one));
        }

        // The sheet's pop scales a centred panel to 1 when open: one that fits a short screen pops to its fitted scale
        // instead, and closed it stays the pop's start of that
        [Test]
        public void A_fitted_centre_panel_opens_at_its_fitted_scale()
        {
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 400f);
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            var fit = sheet.Panel.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            Assert.That(fit.Scale, Is.LessThan(1f), "the panel is taller than the screen");

            sheet.SetOpen(true, false);
            Assert.That(sheet.Panel.localScale.x, Is.EqualTo(fit.Scale).Within(0.0001f), "open");

            sheet.SetOpen(false, false);
            Assert.That(sheet.Panel.localScale.x, Is.EqualTo(0.85f * fit.Scale).Within(0.0001f), "closed");
        }

        // A modal card stretched across its parent ModalInset (60) units from the sides, height units high, centred
        private RectTransform StretchedCard(RectTransform parent, float height)
        {
            var card = Box("Card", parent, Vector2.zero);
            card.anchorMin = new Vector2(0f, 0.5f);
            card.anchorMax = new Vector2(1f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(-120f, height);
            card.anchoredPosition = Vector2.zero;
            return card;
        }

        private static void SetBounded(KitFitScale fit, bool bounded)
        {
            var serialized = new SerializedObject(fit);
            serialized.FindProperty("bounded").boolValue = bounded;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Fitted_card_spans_86_percent_of_a_portrait_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 1920f));
            var card = StretchedCard(parent, 900f);
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.width, Is.EqualTo(KitFitScale.PortraitWidth * 1080f).Within(0.5f), "width");
            Assert.That(card.rect.width * card.localScale.x, Is.LessThanOrEqualTo(KitFitScale.PortraitWidth * 1080f + 0.5f), "shown width");
            Assert.That(fit.Scale, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Centre(card).x, Is.EqualTo(Centre(parent).x).Within(0.5f), "centred");
        }

        [Test]
        public void Fitted_card_spans_58_percent_of_a_landscape_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(2147f, 966f));
            var card = StretchedCard(parent, 883f);
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(card.rect.width, Is.EqualTo(KitFitScale.LandscapeWidth * 2147f).Within(0.5f), "width");
            Assert.That(card.rect.width * card.localScale.x, Is.LessThanOrEqualTo(KitFitScale.LandscapeWidth * 2147f + 0.5f), "shown width");
            Assert.That(card.rect.height * card.localScale.y, Is.LessThanOrEqualTo(KitFitScale.LandscapeHeight * 966f + 0.01f), "shown height");
            Assert.That(fit.Scale, Is.LessThan(1f), "the height bound scales it down");
            Assert.That(Centre(card).x, Is.EqualTo(Centre(parent).x).Within(0.5f), "centred");
        }

        // The settings card opts out of the shares (its controls stay touch-sized): a self-sized card the 58 % share would
        // shrink keeps the margin fit
        [Test]
        public void Unbounded_card_keeps_the_margin_fit()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(2147f, 966f));
            var card = Box("Card", parent, new Vector2(1900f, 700f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            Assert.That(fit.Bounded, Is.True, "bounded by default");
            Assert.That(fit.Scale * 1900f, Is.EqualTo(KitFitScale.LandscapeWidth * 2147f).Within(0.5f), "bounded: 58 %");

            SetBounded(fit, false);
            fit.Fit();

            Assert.That(fit.Bounded, Is.False);
            Assert.That(fit.Scale, Is.EqualTo(Mathf.Clamp01((966f - 2f * KitLayout.Gap) / 700f)).Within(0.0001f), "the margin fit");
            Assert.That(card.rect.width, Is.EqualTo(1900f).Within(0.01f), "keeps its own width");
            Assert.That(card.localScale.x, Is.EqualTo(fit.Scale).Within(0.0001f));
        }

        // A game's own window built at run time may opt out of the shares: the card refits at once, to the margin fit and
        // the sides it had before the shares set them, and opting in again bounds it again
        [Test]
        public void SetBounded_refits_the_card()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(2147f, 966f));
            var card = StretchedCard(parent, 900f);
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            Assert.That(card.rect.width, Is.EqualTo(KitFitScale.LandscapeWidth * 2147f).Within(0.5f), "bounded: 58 %");
            var bounded = fit.Scale;
            Assert.That(bounded, Is.EqualTo(KitFitScale.LandscapeHeight * 966f / 900f).Within(0.0001f), "bounded: 88 % high");

            fit.SetBounded(false);
            Assert.That(fit.Bounded, Is.False);
            Assert.That(card.rect.width, Is.EqualTo(2147f - 120f).Within(0.5f), "its own sides again");
            Assert.That(fit.Scale, Is.EqualTo(Mathf.Clamp01((966f - 2f * KitLayout.Gap) / 900f)).Within(0.0001f), "the margin fit");
            Assert.That(card.localScale.x, Is.EqualTo(fit.Scale).Within(0.0001f));

            fit.SetBounded(true);
            Assert.That(fit.Bounded, Is.True);
            Assert.That(card.rect.width, Is.EqualTo(KitFitScale.LandscapeWidth * 2147f).Within(0.5f), "bounded again");
            Assert.That(fit.Scale, Is.EqualTo(bounded).Within(0.0001f));
        }

        [Test]
        public void Tall_card_scales_to_80_percent_of_a_portrait_parent()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 1920f));
            var card = Box("Card", parent, new Vector2(900f, 1700f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();

            Assert.That(fit.Scale * 1700f, Is.EqualTo(KitFitScale.PortraitHeight * 1920f).Within(0.5f));
            Assert.That(card.localScale.y, Is.EqualTo(fit.Scale).Within(0.0001f));
        }

        // Turned to landscape and back while shown, the card ends exactly where it started
        [Test]
        public void Card_refits_when_the_parent_turns()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1080f, 1920f));
            var card = StretchedCard(parent, 900f);
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            var offsetMin = card.offsetMin;
            var offsetMax = card.offsetMax;
            var scale = card.localScale;

            parent.sizeDelta = new Vector2(2147f, 966f);
            fit.Fit();
            Assert.That(card.rect.width, Is.EqualTo(KitFitScale.LandscapeWidth * 2147f).Within(0.5f), "landscape width");
            Assert.That(card.rect.height * card.localScale.y, Is.LessThanOrEqualTo(KitFitScale.LandscapeHeight * 966f + 0.01f),
                "landscape height");

            parent.sizeDelta = new Vector2(1080f, 1920f);
            fit.Fit();
            Assert.That(Vector2.Distance(card.offsetMin, offsetMin), Is.LessThan(0.01f), $"offsetMin {card.offsetMin}, first {offsetMin}");
            Assert.That(Vector2.Distance(card.offsetMax, offsetMax), Is.LessThan(0.01f), $"offsetMax {card.offsetMax}, first {offsetMax}");
            Assert.That(Vector3.Distance(card.localScale, scale), Is.LessThan(0.0001f), $"scale {card.localScale}, first {scale}");
        }

        // A self-sized card's rect stays as it is when its parent only narrows: the fit before the canvas draws looks at
        // the parent's width too, not only its height
        [Test]
        public void Fit_follows_a_parent_that_only_changes_width()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(2147f, 966f));
            var card = Box("Card", parent, new Vector2(1900f, 700f));
            var fit = card.gameObject.AddComponent<KitFitScale>();
            fit.Fit();
            var before = fit.Scale;

            parent.sizeDelta = new Vector2(1600f, 966f);
            Canvas.ForceUpdateCanvases();

            Assert.That(fit.Scale, Is.LessThan(before));
            Assert.That(fit.Scale * 1900f, Is.EqualTo(KitFitScale.LandscapeWidth * 1600f).Within(0.5f));
        }
    }
}
