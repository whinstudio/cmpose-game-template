using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // The coin packs' grid: as wide as its column gives it (it asks for no width of its own), its constraintCount
    // columns sharing that width less the padding and spacing, so the cells narrow with the screen. The cells' height
    // stays cellSize.y.
    public sealed class ShopPackGrid : GridLayoutGroup
    {
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal, 1f, 0);
        }

        public override void SetLayoutHorizontal()
        {
            FitCells();
            base.SetLayoutHorizontal();
        }

        public override void SetLayoutVertical()
        {
            FitCells();
            base.SetLayoutVertical();
        }

        // Set on the field, not cellSize: the property would ask for another layout pass while this one runs
        private void FitCells()
        {
            var columns = constraintCount < 1 ? 1 : constraintCount;
            var free = rectTransform.rect.width - padding.horizontal - spacing.x * (columns - 1);
            m_CellSize.x = free > 0f ? free / columns : 0f;
        }
    }
}
