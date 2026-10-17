using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // A slow breath in and out (the Play button while it waits), on unscaled time; rests while a PressFeedback on the
    // same object is busy
    public sealed class IdlePulse : KitElement
    {
        [SerializeField, Range(0f, 0.2f)] private float amplitude = 0.04f;
        private PressFeedback press;
        private float phase;

        public float Amplitude => amplitude;

        private void Awake() => press = GetComponent<PressFeedback>();

        private void Update()
        {
            if (press != null && press.Busy)
            {
                phase = 0f;
                return;
            }
            phase += Time.unscaledDeltaTime / Mathf.Max(Timings.Breath, 0.1f);
            if (phase >= 1f) phase -= 1f;
            var value = 1f + amplitude * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * phase));
            transform.localScale = new Vector3(value, value, 1f);
        }

        private void OnDisable()
        {
            phase = 0f;
            transform.localScale = Vector3.one;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
        }
    }
}
