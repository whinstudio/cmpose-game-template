using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Lays its children out left to right, gap apart, at their preferred sizes: a row too narrow for them all scales them
    // down alike (localScale, never below MinScale), and those that still do not fit wrap to further lines, gap between
    // lines. Its preferred height is the lines' total, so a parent layout makes room for a wrapped row.
    public sealed class KitFlowRow : LayoutGroup
    {
        // The smallest scale before items wrap: smaller would make their text too small to read
        public const float MinScale = 0.75f;

        [SerializeField] private float gap = KitLayout.Gap;

        private readonly List<int> lineStarts = new List<int>();
        private readonly List<float> lineWidths = new List<float>();
        private readonly List<float> lineHeights = new List<float>();
        private float scale = 1f;

        public float Gap => gap;
        // The children's scale at the last layout
        public float Scale => scale;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            var widest = 0f;
            var total = 0f;
            foreach (var child in rectChildren)
            {
                var width = LayoutUtility.GetPreferredWidth(child);
                widest = Mathf.Max(widest, width);
                total += width;
            }
            if (rectChildren.Count > 1) total += gap * (rectChildren.Count - 1);
            SetLayoutInputForAxis(padding.horizontal + widest * MinScale, padding.horizontal + total, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            Lines();
            var height = Height();
            SetLayoutInputForAxis(padding.vertical + height, padding.vertical + height, -1f, 1);
        }

        public override void SetLayoutHorizontal() => Place(0);

        public override void SetLayoutVertical() => Place(1);

        // Splits the children into lines at the scale the row's width gives them
        private void Lines()
        {
            lineStarts.Clear();
            lineWidths.Clear();
            lineHeights.Clear();
            var count = rectChildren.Count;
            if (count == 0) return;
            var available = rectTransform.rect.width - padding.horizontal;
            var total = 0f;
            foreach (var child in rectChildren) total += LayoutUtility.GetPreferredWidth(child);
            scale = total > 0f ? Mathf.Clamp((available - gap * (count - 1)) / total, MinScale, 1f) : 1f;
            var width = 0f;
            for (var i = 0; i < count; i++)
            {
                var child = rectChildren[i];
                var itemWidth = LayoutUtility.GetPreferredWidth(child) * scale;
                var itemHeight = LayoutUtility.GetPreferredHeight(child) * scale;
                var line = lineStarts.Count - 1;
                if (line < 0 || width + gap + itemWidth > available + 0.01f)
                {
                    lineStarts.Add(i);
                    lineWidths.Add(itemWidth);
                    lineHeights.Add(itemHeight);
                    width = itemWidth;
                    continue;
                }
                width += gap + itemWidth;
                lineWidths[line] = width;
                lineHeights[line] = Mathf.Max(lineHeights[line], itemHeight);
            }
        }

        private float Height()
        {
            var height = 0f;
            foreach (var line in lineHeights) height += line;
            if (lineHeights.Count > 1) height += gap * (lineHeights.Count - 1);
            return height;
        }

        // Each line placed by childAlignment across, the lines together by it down; an item centred in its line's height
        private void Place(int axis)
        {
            Lines();
            var y = GetStartOffset(1, Height());
            for (var line = 0; line < lineStarts.Count; line++)
            {
                var end = line + 1 < lineStarts.Count ? lineStarts[line + 1] : rectChildren.Count;
                var x = GetStartOffset(0, lineWidths[line]);
                for (var i = lineStarts[line]; i < end; i++)
                {
                    var child = rectChildren[i];
                    var width = LayoutUtility.GetPreferredWidth(child);
                    var height = LayoutUtility.GetPreferredHeight(child);
                    var value = new Vector3(scale, scale, 1f);
                    if (child.localScale != value) child.localScale = value;
                    if (axis == 0) SetChildAlongAxisWithScale(child, 0, x, width, scale);
                    else SetChildAlongAxisWithScale(child, 1, y + (lineHeights[line] - height * scale) / 2f, height, scale);
                    x += width * scale + gap;
                }
                y += lineHeights[line] + gap;
            }
        }
    }
}
