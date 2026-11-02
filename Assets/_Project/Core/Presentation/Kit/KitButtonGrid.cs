using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Lays its shown children out in order in two columns of equal width, Spacing apart, each cell CellHeight high; with
    // an odd count the last one spans both columns. Hidden children leave no gap. Its preferred height is the rows'
    // (rows × CellHeight, Spacing between them, padding), 0 without children, so a parent column makes room for it.
    public sealed class KitButtonGrid : LayoutGroup
    {
        public float Spacing = KitLayout.Gap;
        public float CellHeight = KitLayout.MinTouch;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var height = Height();
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            var count = rectChildren.Count;
            var width = rectTransform.rect.width - padding.horizontal;
            var cell = (width - Spacing) / 2f;
            for (var i = 0; i < count; i++)
            {
                var child = rectChildren[i];
                if (i == count - 1 && count % 2 == 1) SetChildAlongAxis(child, 0, padding.left, width);
                else SetChildAlongAxis(child, 0, padding.left + i % 2 * (cell + Spacing), cell);
            }
        }

        public override void SetLayoutVertical()
        {
            var y = GetStartOffset(1, Height() - (rectChildren.Count > 0 ? padding.vertical : 0f));
            for (var i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 1, y + i / 2 * (CellHeight + Spacing), CellHeight);
        }

        // The rows' height with the padding; 0 without shown children
        private float Height()
        {
            var rows = (rectChildren.Count + 1) / 2;
            if (rows == 0) return 0f;
            return padding.vertical + rows * CellHeight + (rows - 1) * Spacing;
        }
    }
}
