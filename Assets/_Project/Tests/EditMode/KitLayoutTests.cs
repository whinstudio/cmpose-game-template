using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The kit's layout rules: a round icon in the middle of its button (a dressed one on the sprite's visible face), a
    // touch area of at least KitLayout.MinTouch, a row of items KitLayout.Gap apart that shrinks before it wraps, and a
    // card scaled down to fit its parent's height.
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

        // A sliced 48 x height sprite at 100 px per unit
        private Sprite Sprite(string name, int height)
        {
            var texture = new Texture2D(48, height) { name = name };
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, 48f, height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
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
            Assert.That(Centre(button.Label.rectTransform).y - Centre(button.Body.rectTransform).y, Is.EqualTo(8f).Within(0.5f));

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

        [Test]
        public void Fit_scale_shrinks_a_tall_card()
        {
            var parent = Box("Screen", canvas.transform, new Vector2(1920f, 1080f));
            var card = Box("Card", parent, new Vector2(900f, 1500f));
            var fit = card.gameObject.AddComponent<KitFitScale>();

            Assert.That(fit.Margin, Is.EqualTo(KitLayout.Gap));
            Assert.That(card.localScale.x, Is.EqualTo((1080f - 2f * KitLayout.Gap) / 1500f).Within(0.0001f));
            Assert.That(card.localScale.y, Is.EqualTo(card.localScale.x));

            card.sizeDelta = new Vector2(900f, 900f);
            Assert.That(card.localScale, Is.EqualTo(Vector3.one), "fits: full size");

            // A shorter parent shrinks it again
            parent.sizeDelta = new Vector2(1920f, 600f);
            fit.Fit();
            Assert.That(card.localScale.x, Is.EqualTo((600f - 2f * KitLayout.Gap) / 900f).Within(0.0001f));
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
    }
}
