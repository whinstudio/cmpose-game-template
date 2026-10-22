using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // A game skin's card frame is drawn over the card's edges (9-slice border × sliceScale units): the device check found
    // the settings rows on a pixel skin's frame, the column's padding (40) thinner than the frame (about 60). Every
    // card keeps its content FrameGap inside the frame's inner edge, and a frame without a border changes nothing.
    public sealed class CardInsetTests
    {
        // The thick frame these tests draw: 30 source pixels at sliceScale 3, 90 card units
        private const float Thick = 30f;
        private const float Frame = 90f;
        // KitCard keeps content this far inside the frame's inner edge
        private const float Gap = 12f;
        private const string SettingsPrefab = "Assets/_Project/App/Settings/SettingsPanel.prefab";
        private const string SettingsColumn = "Panel/SafeArea/Card";
        private const string EconomyPrefab = "Assets/_Project/Features/Economy/Resources/Economy/EconomyWindows.prefab";

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        // The pixel skin of SkinSprites.Json at sliceScale 3, its panels border source pixels thick on every side
        private void Skin(float border)
        {
            var json = SkinSprites.Json.Replace("\"sliceScale\":4", "\"sliceScale\":3");
            var sprites = new Dictionary<string, Sprite>();
            GameSkin.ForTests(GameSkin.Parse(json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = Panel(path, border);
                return sprite;
            }, _ => null));
        }

        private Sprite Panel(string name, float border) => Panel(name, new Vector4(border, border, border, border));

        // border: source pixels (left, bottom, right, top)
        private Sprite Panel(string name, Vector4 border)
        {
            var texture = new Texture2D(96, 96) { name = name };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                border);
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        private FakeThemeSource Theme()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            return new FakeThemeSource { Current = palette, Style = ThemeStyle.Load() };
        }

        // A copy of a prefab laid out as a 20:9 phone (1080 x 2400), every child shown, bound to a theme (LayoutStage)
        private GameObject Stage(string path) => LayoutStage.Prefab(path, new Vector2(1080f, 2400f), Theme(), made);

        // A rect in the card's own units
        private static Rect In(RectTransform card, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = card.InverseTransformPoint(corners[0]);
            var max = card.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertInside(RectTransform card, RectTransform rect, float inset, string what, bool vertical = true)
        {
            var box = card.rect;
            var r = In(card, rect);
            Assert.That(r.xMin, Is.GreaterThanOrEqualTo(box.xMin + inset - 0.5f), $"{what}: left");
            Assert.That(r.xMax, Is.LessThanOrEqualTo(box.xMax - inset + 0.5f), $"{what}: right");
            if (!vertical) return;
            Assert.That(r.yMin, Is.GreaterThanOrEqualTo(box.yMin + inset - 0.5f), $"{what}: bottom");
            Assert.That(r.yMax, Is.LessThanOrEqualTo(box.yMax - inset + 0.5f), $"{what}: top");
        }

        private static IEnumerable<RectTransform> ColumnItems(Transform card)
        {
            foreach (Transform child in card)
            {
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                yield return (RectTransform)child;
            }
        }

        [Test]
        public void Settings_rows_stay_inside_a_thick_skin_frame()
        {
            Skin(Thick);
            var card = (RectTransform)Stage(SettingsPrefab).transform.Find(SettingsColumn);

            var items = 0;
            foreach (var item in ColumnItems(card))
            {
                AssertInside(card, item, Frame + Gap, item.name);
                items++;
            }
            Assert.That(items, Is.GreaterThan(8), "the column's rows");
        }

        [Test]
        public void Settings_keep_their_padding_on_a_frame_without_a_border()
        {
            Skin(0f);
            var card = Stage(SettingsPrefab).transform.Find(SettingsColumn);

            var padding = card.GetComponent<VerticalLayoutGroup>().padding;
            Assert.That((padding.left, padding.right, padding.top, padding.bottom), Is.EqualTo((40, 40, 56, 56)));
        }

        [Test]
        public void Economy_window_content_stays_inside_a_thick_skin_frame()
        {
            Skin(Thick);
            var sheets = Stage(EconomyPrefab).GetComponentsInChildren<KitSheet>(true);

            Assert.That(sheets, Is.Not.Empty);
            foreach (var sheet in sheets) AssertInside(sheet.Panel, sheet.Content, Frame + Gap, sheet.name);
        }

        // Each side reads its own border: Sprite.border is (left, bottom, right, top), RectOffset (left, right, top,
        // bottom), so a swap would move one side's frame onto another
        [Test]
        public void Each_side_clears_its_own_frame()
        {
            var json = SkinSprites.Json.Replace("\"sliceScale\":4", "\"sliceScale\":3");
            var panel = Panel("panel", new Vector4(10f, 20f, 30f, 40f));
            GameSkin.ForTests(GameSkin.Parse(json, _ => panel, _ => null));
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            var card = KitFactory.Card(canvas.transform, "Card");
            var content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(card.transform, false);

            card.Pad(new RectOffset(40, 40, 56, 56), content);

            Assert.That(card.Frame, Is.EqualTo(new Vector4(30f, 60f, 90f, 120f)));
            var inset = card.ContentPadding;
            Assert.That((inset.left, inset.bottom, inset.right, inset.top), Is.EqualTo((42, 72, 102, 132)));
            Assert.That(content.offsetMin, Is.EqualTo(new Vector2(42f, 72f)));
            Assert.That(content.offsetMax, Is.EqualTo(new Vector2(-102f, -132f)));
        }

        [Test]
        public void A_sheet_keeps_its_padding_on_a_frame_without_a_border_and_clears_a_thick_one()
        {
            Skin(0f);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            var plain = KitFactory.Sheet(canvas.transform, "Plain", SheetPlacement.Bottom, 900f);
            plain.Bind(Theme());
            Assert.That(plain.Content.offsetMin, Is.EqualTo(new Vector2(KitFactory.SheetPadding, KitFactory.SheetOverhang + KitFactory.SheetPadding)));
            Assert.That(plain.Content.offsetMax, Is.EqualTo(new Vector2(-KitFactory.SheetPadding, -KitFactory.SheetPadding)));

            Skin(Thick);
            var framed = KitFactory.Sheet(canvas.transform, "Framed", SheetPlacement.Center, 900f);
            framed.Bind(Theme());
            Assert.That(framed.Content.offsetMin, Is.EqualTo(new Vector2(Frame + Gap, Frame + Gap)));
            Assert.That(framed.Content.offsetMax, Is.EqualTo(new Vector2(-(Frame + Gap), -(Frame + Gap))));
        }
    }
}
