using UnityEngine;
using UnityEngine.EventSystems;

namespace Kturker.UnityBase.Core.Presentation
{
    // Scales a box (a card) down, never up, so it fits its parent's height less margin above and below: a long card on a
    // landscape screen. Fitted again when the box resizes and, before the canvas draws, when the parent has.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitFitScale : UIBehaviour
    {
        [SerializeField] private float margin = KitLayout.Gap;
        private float parentHeight = -1f;
        private float scale = 1f;
        // Scaling the box may call back here: one fit at a time
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
            parentHeight = parent.rect.height;
            scale = Mathf.Clamp01((parentHeight - 2f * margin) / height);
            var value = new Vector3(scale, scale, 1f);
            if (rect.localScale == value) return;
            fitting = true;
            try
            {
                rect.localScale = value;
            }
            finally
            {
                fitting = false;
            }
        }

        // The parent's resizes reach the box only when they move its own edges: look before each draw
        private void Check()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null && !Mathf.Approximately(parent.rect.height, parentHeight)) Fit();
        }
    }
}
