using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // Buttons in two equal columns, Spacing apart, an odd last one across both; hidden ones leave no gap and the grid
    // asks for its rows' height.
    public sealed class KitButtonGridTests
    {
        private const float GridWidth = 900f;

        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, 1920f);
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        // Left, bottom, right, top in world units (the canvas is unscaled: canvas units)
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // A grid GridWidth wide (1000 high, top-anchored layout) holding count buttons
        private List<RectTransform> Grid(int count, out KitButtonGrid grid, float cellHeight = 140f)
        {
            var go = new GameObject("Grid", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(GridWidth, 1000f);
            grid = go.AddComponent<KitButtonGrid>();
            grid.CellHeight = cellHeight;
            var items = new List<RectTransform>();
            for (var i = 0; i < count; i++)
            {
                var item = new GameObject($"Button{i}", typeof(RectTransform));
                item.transform.SetParent(rect, false);
                items.Add((RectTransform)item.transform);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return items;
        }

        private static void Rebuild(KitButtonGrid grid) => LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);

        [Test]
        public void Four_buttons_make_two_equal_rows()
        {
            var items = Grid(4, out var grid);
            var cell = (GridWidth - grid.Spacing) / 2f;

            foreach (var item in items)
            {
                Assert.That(item.rect.width, Is.EqualTo(cell).Within(0.01f), item.name);
                Assert.That(item.rect.height, Is.EqualTo(140f).Within(0.01f), item.name);
            }
            var b = items.ConvertAll(Bounds);
            // Same row: one top, Spacing between the columns; next row Spacing under it
            Assert.That(b[1].yMax, Is.EqualTo(b[0].yMax).Within(0.01f));
            Assert.That(b[1].xMin - b[0].xMax, Is.EqualTo(grid.Spacing).Within(0.01f));
            Assert.That(b[2].xMin, Is.EqualTo(b[0].xMin).Within(0.01f));
            Assert.That(b[3].xMin, Is.EqualTo(b[1].xMin).Within(0.01f));
            Assert.That(b[0].yMin - b[2].yMax, Is.EqualTo(grid.Spacing).Within(0.01f));
            Assert.That(b[3].yMax, Is.EqualTo(b[2].yMax).Within(0.01f));
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.transform),
                Is.EqualTo(2f * 140f + grid.Spacing).Within(0.01f));
        }

        [Test]
        public void An_odd_last_button_spans_both_columns()
        {
            var items = Grid(5, out var grid);
            var b = items.ConvertAll(Bounds);

            Assert.That(b[4].xMin, Is.EqualTo(b[0].xMin).Within(0.01f));
            Assert.That(b[4].xMax, Is.EqualTo(b[1].xMax).Within(0.01f));
            Assert.That(items[4].rect.width, Is.EqualTo(GridWidth).Within(0.01f));
            Assert.That(b[2].yMin - b[4].yMax, Is.EqualTo(grid.Spacing).Within(0.01f), "a row of its own");
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.transform),
                Is.EqualTo(3f * 140f + 2f * grid.Spacing).Within(0.01f));

            // A single button is a full-width row
            var single = Grid(1, out _);
            Assert.That(single[0].rect.width, Is.EqualTo(GridWidth).Within(0.01f));
        }

        [Test]
        public void Hidden_children_leave_no_gap()
        {
            var items = Grid(4, out var grid);
            items[1].gameObject.SetActive(false);
            Rebuild(grid);
            var b = items.ConvertAll(Bounds);

            // 0 and 2 share the first row, 3 spans the second
            Assert.That(b[2].yMax, Is.EqualTo(b[0].yMax).Within(0.01f));
            Assert.That(b[2].xMin - b[0].xMax, Is.EqualTo(grid.Spacing).Within(0.01f));
            Assert.That(b[0].yMin - b[3].yMax, Is.EqualTo(grid.Spacing).Within(0.01f));
            Assert.That(items[3].rect.width, Is.EqualTo(GridWidth).Within(0.01f));
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.transform),
                Is.EqualTo(2f * 140f + grid.Spacing).Within(0.01f));
        }

        [Test]
        public void No_children_prefer_zero_height()
        {
            Grid(0, out var grid);
            grid.padding = new RectOffset(10, 10, 20, 20);
            Rebuild(grid);
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.transform), Is.EqualTo(0f));

            // Only hidden ones: the same
            var items = Grid(2, out var hiddenGrid);
            foreach (var item in items) item.gameObject.SetActive(false);
            Rebuild(hiddenGrid);
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)hiddenGrid.transform), Is.EqualTo(0f));
        }

        [Test]
        public void Padding_counts()
        {
            var items = Grid(3, out var grid);
            grid.padding = new RectOffset(30, 50, 20, 40);
            Rebuild(grid);
            var cell = (GridWidth - 80f - grid.Spacing) / 2f;

            Assert.That(items[0].rect.width, Is.EqualTo(cell).Within(0.01f));
            Assert.That(items[2].rect.width, Is.EqualTo(GridWidth - 80f).Within(0.01f));
            var box = Bounds((RectTransform)grid.transform);
            Assert.That(Bounds(items[0]).xMin - box.xMin, Is.EqualTo(30f).Within(0.01f));
            Assert.That(box.yMax - Bounds(items[0]).yMax, Is.EqualTo(20f).Within(0.01f));
            Assert.That(LayoutUtility.GetPreferredHeight((RectTransform)grid.transform),
                Is.EqualTo(60f + 2f * 140f + grid.Spacing).Within(0.01f));
        }

        [Test]
        public void Cells_are_at_least_the_touch_target()
        {
            var items = Grid(6, out _, KitLayout.MinTouch);

            foreach (var item in items)
            {
                Assert.That(item.rect.height, Is.GreaterThanOrEqualTo(KitLayout.MinTouch), item.name);
                Assert.That(item.rect.width, Is.GreaterThanOrEqualTo(KitLayout.MinTouch), item.name);
            }
        }
    }
}
