using UnityEngine;
using UnityEngine.EventSystems;

namespace Kturker.UnityBase.Core.Presentation
{
    // The invisible Hit child KitLayout.AddTouchArea makes: as big as its parent, and KitLayout.MinTouch on an axis where
    // the parent is smaller, centred on it. Stretched over the parent, so it follows the parent as it resizes.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitTouchArea : UIBehaviour
    {
        // Setting the rect calls back here: one fit at a time
        private bool fitting;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            if (!fitting) Fit();
        }

        public void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null || fitting) return;
            var size = parent.rect.size;
            var grow = new Vector2(Mathf.Max(0f, KitLayout.MinTouch - size.x), Mathf.Max(0f, KitLayout.MinTouch - size.y));
            fitting = true;
            try
            {
                if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
                if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
                if (rect.pivot != new Vector2(0.5f, 0.5f)) rect.pivot = new Vector2(0.5f, 0.5f);
                if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
                if (rect.sizeDelta != grow) rect.sizeDelta = grow;
            }
            finally
            {
                fitting = false;
            }
        }
    }
}
