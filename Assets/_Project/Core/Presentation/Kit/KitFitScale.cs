using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Fits a box (a card) to its parent's height. A fixed-height box grows with a parent taller than the reference
    // screen, as a band of it would; any box scales down, never up, so it fits the parent's height less margin above and
    // below: a long card on a landscape screen. Fitted again when the box resizes and, before the canvas draws, when the
    // parent has.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitFitScale : UIBehaviour
    {
        // The reference screen's height in canvas units (CanvasScaler 1080 x 1920)
        public const float ReferenceHeight = 1920f;

        [SerializeField] private float margin = KitLayout.Gap;
        // A fixed-height box's height on the reference screen; 0 until the first fit takes its authored height
        [SerializeField] private float baseHeight;
        private float parentHeight = -1f;
        private float scale = 1f;
        // The height the last fit gave the box: any other height was set by someone else and becomes the base
        private float fittedHeight = -1f;
        // Growing or scaling the box may call back here: one fit at a time
        private bool fitting;

        public float Margin => margin;
        // The scale the last fit gave the box (KitSheet pops a centred panel to it)
        public float Scale => scale;

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.willRenderCanvases += Check;
            Fit();
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= Check;
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            var height = rect.rect.height;
            if (parent == null || height <= 0f || fitting) return;
            // Not laid out yet: a prefab built off-screen would save the card at scale 0 (Check fits it once it has a height)
            if (parent.rect.height <= 0f) return;
            parentHeight = parent.rect.height;
            fitting = true;
            try
            {
                if (FixedHeight(rect)) height = Grow(rect, height);
                scale = Mathf.Clamp01((parentHeight - 2f * margin) / height);
                var value = new Vector3(scale, scale, 1f);
                if (rect.localScale != value) rect.localScale = value;
            }
            finally
            {
                fitting = false;
            }
        }

        // The box becomes its base height times how much taller than the reference the parent is (never shorter), around
        // its middle anchor
        private float Grow(RectTransform rect, float height)
        {
            // A box saved while grown kept its base: trusted until this one has fitted, then a height it did not give wins
            if (baseHeight <= 0f || (fittedHeight > 0f && !Mathf.Approximately(height, fittedHeight))) baseHeight = height;
            var target = baseHeight * Mathf.Max(1f, parentHeight / ReferenceHeight);
            if (!Mathf.Approximately(height, target)) rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + target - height);
            fittedHeight = target;
            return target;
        }

        // Not sized by itself (the Settings card's ContentSizeFitter, an AspectRatioFitter), not stretched with its parent,
        // not sized by a layout group
        private static bool FixedHeight(RectTransform rect)
        {
            if (rect.TryGetComponent<ILayoutSelfController>(out _)) return false;
            if (!Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y)) return false;
            if (!rect.parent.TryGetComponent<LayoutGroup>(out var group) || !group.isActiveAndEnabled) return true;
            if (rect.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout) return true;
            return group is HorizontalOrVerticalLayoutGroup row && !row.childControlHeight;
        }

        // The parent's resizes reach the box only when they move its own edges: look before each draw
        private void Check()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null && !Mathf.Approximately(parent.rect.height, parentHeight)) Fit();
        }
    }
}
