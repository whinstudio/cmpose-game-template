using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // The kit's layout rules: items stand Gap apart (KitFlowRow, KitFitScale's margin) and every control takes touches
    // over at least MinTouch on each axis (KitTouchArea).
    public static class KitLayout
    {
        // Between neighbours, between lines and from a screen's edge, in reference units
        public const float Gap = 16f;
        // 48 dp on the 1080-wide reference canvas of a 411 dp phone (the strictest of the sizes the layout tests check)
        public const float MinTouch = 126f;

        // A Hit child under target that takes its touches over at least MinTouch on each axis (target's own size where
        // it is bigger); the same one again if target has it already
        public static KitTouchArea AddTouchArea(Selectable target)
        {
            var existing = target.GetComponentInChildren<KitTouchArea>(true);
            if (existing != null && existing.transform.parent == target.transform) return existing;
            var go = new GameObject("Hit", typeof(RectTransform));
            go.transform.SetParent(target.transform, false);
            go.transform.SetAsFirstSibling();
            var image = go.AddComponent<Image>();
            image.raycastTarget = true;
            image.color = Color.clear;
            var themed = go.AddComponent<ThemedGraphic>();
            themed.SetRole(ThemeRole.Picture);
            themed.SetTone(0f, 0f);
            return go.AddComponent<KitTouchArea>();
        }
    }
}
