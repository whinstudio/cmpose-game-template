using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Fits a modal card to its parent (the screen's safe area). A bounded card (the default) stretched across its parent
    // takes PortraitWidth of its width (LandscapeWidth on a landscape parent: wider than high), centred, and stays margin
    // in from its sides at least (a card whose Close X overhangs it keeps the X clear of the screen's sides); a card that
    // sizes itself keeps its width. A fixed-height card grows with a parent taller than the reference screen, as a band of it
    // would. Then any card scales down, never up, so it fits the parent's height less margin above and below and, when
    // bounded, stays within PortraitHeight of it (LandscapeHeight in landscape) and within the width share: content scaled,
    // never clipped. Fitted again when the card resizes and, before the canvas draws, when the parent has (its width or
    // its height). Code that builds a window at run time opts it out of the shares with SetBounded(false).
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitFitScale : UIBehaviour
    {
        // The reference screen's height in canvas units (CanvasScaler 1080 x 1920)
        public const float ReferenceHeight = 1920f;
        // The share of the parent's width and height a card may take, portrait and landscape
        public const float PortraitWidth = 0.86f;
        public const float PortraitHeight = 0.80f;
        public const float LandscapeWidth = 0.58f;
        public const float LandscapeHeight = 0.88f;

        [SerializeField] private float margin = KitLayout.Gap;
        // A fixed-height box's height on the reference screen; 0 until the first fit takes its authored height
        [SerializeField] private float baseHeight;
        // Held to the width and height shares. The settings card opts out (margin fit only): SettingsCardLayout sizes it
        // already, and the shares would scale its controls under the 48 dp touch size (KitLayout.MinTouch) on a tablet.
        // Cards saved before the field existed read true
        [SerializeField] private bool bounded = true;
        private Vector2 parentSize = new Vector2(-1f, -1f);
        private float scale = 1f;
        // The height the last fit gave the box: any other height was set by someone else and becomes the base
        private float fittedHeight = -1f;
        // Growing or scaling the box may call back here: one fit at a time
        private bool fitting;
        // A stretched card's side offsets before the shares first set them (kept for SetBounded(false))
        private bool spanned;
        private float ownLeft, ownRight;

        public float Margin => margin;
        public bool Bounded => bounded;
        // The scale the last fit gave the box (KitSheet pops a centred panel to it)
        public float Scale => scale;

        // Held to the shares or not (the margin fit only): fitted again at once when it changes, a stretched card given back
        // the sides it had before the shares set them
        public void SetBounded(bool value)
        {
            if (bounded == value) return;
            bounded = value;
            if (!bounded && spanned)
            {
                spanned = false;
                var rect = (RectTransform)transform;
                rect.offsetMin = new Vector2(ownLeft, rect.offsetMin.y);
                rect.offsetMax = new Vector2(ownRight, rect.offsetMax.y);
            }
            Fit();
        }

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
            // Not laid out yet: a prefab built off-screen would save the card at scale 0 (Check fits it once it has a size)
            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            parentSize = size;
            fitting = true;
            try
            {
                var landscape = size.x > size.y;
                var widthShare = landscape ? LandscapeWidth : PortraitWidth;
                var heightShare = landscape ? LandscapeHeight : PortraitHeight;
                var stretched = bounded && Stretched(rect);
                if (stretched)
                {
                    if (!spanned)
                    {
                        spanned = true;
                        ownLeft = rect.offsetMin.x;
                        ownRight = rect.offsetMax.x;
                    }
                    Span(rect, Mathf.Max(size.x * (1f - widthShare) / 2f, margin));
                }
                if (FixedHeight(rect)) height = Grow(rect, height);
                var fit = (size.y - 2f * margin) / height;
                if (bounded)
                {
                    fit = Mathf.Min(fit, heightShare * size.y / height);
                    // A stretched card spans its share already
                    var width = rect.rect.width;
                    if (!stretched && width > 0f) fit = Mathf.Min(fit, widthShare * size.x / width);
                }
                scale = Mathf.Clamp01(fit);
                var value = new Vector3(scale, scale, 1f);
                if (rect.localScale != value) rect.localScale = value;
            }
            finally
            {
                fitting = false;
            }
        }

        // Stretched across its parent (every SceneBuilder FittedModal card)
        private static bool Stretched(RectTransform rect) =>
            Mathf.Approximately(rect.anchorMin.x, 0f) && Mathf.Approximately(rect.anchorMax.x, 1f);

        // The card's sides inset units in from its parent's, written only when they differ (a write calls back here)
        private static void Span(RectTransform rect, float inset)
        {
            if (!Mathf.Approximately(rect.offsetMin.x, inset)) rect.offsetMin = new Vector2(inset, rect.offsetMin.y);
            if (!Mathf.Approximately(rect.offsetMax.x, -inset)) rect.offsetMax = new Vector2(-inset, rect.offsetMax.y);
        }

        // The box becomes its base height times how much taller than the reference the parent is (never shorter), around
        // its middle anchor
        private float Grow(RectTransform rect, float height)
        {
            // A box saved while grown kept its base: trusted until this one has fitted, then a height it did not give wins
            if (baseHeight <= 0f || (fittedHeight > 0f && !Mathf.Approximately(height, fittedHeight))) baseHeight = height;
            var target = baseHeight * Mathf.Max(1f, parentSize.y / ReferenceHeight);
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

        // The parent's resizes reach the box only when they move its own edges: look before each draw (a turn to landscape
        // may keep the parent's height and change its width only)
        private void Check()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            var size = parent.rect.size;
            if (!Mathf.Approximately(size.x, parentSize.x) || !Mathf.Approximately(size.y, parentSize.y)) Fit();
        }
    }
}
