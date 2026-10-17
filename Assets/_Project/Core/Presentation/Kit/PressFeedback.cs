using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Shrinks the object under the finger and springs it back on release, on unscaled time (it plays in the pause
    // panel). Sizes and curves come from the theme's motion profile. A non-interactable Selectable on the same object
    // does not move.
    public sealed class PressFeedback : KitElement, IPointerDownHandler, IPointerUpHandler
    {
        private KitTween scale;
        private bool pressed;
        private Selectable selectable;

        // Pressed, or still on its way back
        public bool Busy => pressed || scale.Running;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;
            pressed = true;
            var timings = Timings;
            scale.Start(transform.localScale.x, timings.PressScale, timings.Press, Ease.OutCubic);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            var timings = Timings;
            scale.Start(transform.localScale.x, 1f, timings.Release, timings.ReleaseEase);
        }

        private void Update()
        {
            if (!scale.Step(Time.unscaledDeltaTime)) return;
            var value = scale.Value;
            transform.localScale = new Vector3(value, value, 1f);
        }

        private void OnDisable()
        {
            pressed = false;
            scale.Snap(1f);
            transform.localScale = Vector3.one;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
        }
    }
}
