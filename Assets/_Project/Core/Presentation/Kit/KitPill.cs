using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Keeps a sliced Image a true pill (round ends, a circle when square) at whatever size it has now: Unity shrinks
    // slice borders that do not fit on each axis alone, so one huge radius would draw a long thin box as a lens.
    // KitShape.Round adds it for KitSprites.Pill and turns it off for any other radius.
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class KitPill : UIBehaviour
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            var image = GetComponent<Image>();
            var size = ((RectTransform)transform).rect.size;
            var side = Mathf.Min(size.x, size.y);
            if (!enabled || image.sprite == null || side <= 0f) return;
            var multiplier = KitSprites.RoundedRadius / (side / 2f);
            if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier)) image.pixelsPerUnitMultiplier = multiplier;
        }
    }
}
