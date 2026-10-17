using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // An on/off switch over a Toggle (KitFactory.Switch builds it): a pill track, Primary when on and Control when
    // off, and a round Surface knob with a soft shadow (both under KnobSlot) that slides across on unscaled time. Follows isOn however it was set
    // (SetIsOnWithoutNotify included).
    [RequireComponent(typeof(Toggle))]
    public sealed class KitSwitch : KitElement
    {
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knobSlot;
        [SerializeField] private Image knob;
        [SerializeField] private Image knobShadow;
        // Gap between the knob and the track's ends, units
        private const float Inset = 8f;
        private Toggle toggle;
        private KitTween travel;
        private bool shown;
        private bool placed;

        public Image Track => track;
        public Image Knob => knob;
        public Toggle Toggle => toggle != null ? toggle : toggle = GetComponent<Toggle>();
        // 0 off .. 1 on, where the knob is now
        public float Position => travel.Value;

        internal void Init(Image trackImage, RectTransform slot, Image knobImage, Image knobShadowImage)
        {
            track = trackImage;
            knobSlot = slot;
            knob = knobImage;
            knobShadow = knobShadowImage;
        }

        private void OnEnable() => Place(Toggle.isOn, false);

        private void Update()
        {
            if (Toggle.isOn != shown) Place(Toggle.isOn, true);
            if (travel.Step(Time.unscaledDeltaTime)) MoveKnob();
        }

        private void Place(bool on, bool animate)
        {
            shown = on;
            var timings = Timings;
            if (animate && placed) travel.Start(travel.Value, on ? 1f : 0f, timings.Release, timings.ReleaseEase);
            else travel.Snap(on ? 1f : 0f);
            placed = true;
            Paint();
            MoveKnob();
        }

        private void MoveKnob()
        {
            if (knobSlot == null) return;
            // The slot's anchors run from the left end (0) to the right end (1) of the track
            var x = travel.Value;
            var rect = knobSlot;
            rect.anchorMin = new Vector2(x, 0.5f);
            rect.anchorMax = new Vector2(x, 0.5f);
            rect.pivot = new Vector2(x, 0.5f);
            rect.anchoredPosition = new Vector2(Mathf.Lerp(Inset, -Inset, x), 0f);
        }

        private void Paint()
        {
            if (track == null) return;
            var role = shown ? ThemeRole.Primary : ThemeRole.Control;
            var themed = Themed(track);
            if (themed.Role != role) themed.SetRole(role);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (track == null || knob == null) return;
            shown = Toggle.isOn;
            if (!travel.Running) travel.Snap(shown ? 1f : 0f);
            KitShape.Round(track, KitSprites.Pill);
            KitShape.Round(knob, KitSprites.Pill);
            var knobRole = Themed(knob);
            if (knobRole.Role != ThemeRole.Surface) knobRole.SetRole(ThemeRole.Surface);
            if (knobShadow != null)
            {
                var strength = style != null ? Mathf.Max(style.ShadowStrength, 0.25f) : 0.25f;
                KitShape.SoftShadow(knobShadow, 30f, strength);
            }
            Paint();
            MoveKnob();
        }
    }
}
