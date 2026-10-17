using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Tap to start" over a finger dot that taps while a ring ripples out of it, on unscaled time; hidden until Show
    public sealed class StartHintView : MonoBehaviour, IStartHintView
    {
        private const float Cycle = 1.4f;
        private const float RingGrow = 1.1f;
        private const float TapDepth = 0.16f;

        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform dot;
        [SerializeField] private RectTransform ring;
        [SerializeField] private CanvasGroup ringGroup;
        private float phase;

        public bool IsShown => content.activeSelf;

        private void Awake() => content.SetActive(false);

        public void Show()
        {
            phase = 0f;
            content.SetActive(true);
        }

        public void Hide() => content.SetActive(false);

        // Steps the tap by dt seconds (Update calls it with the unscaled delta)
        public void Advance(float dt)
        {
            if (!content.activeSelf) return;
            phase += dt / Cycle;
            if (phase >= 1f) phase -= Mathf.Floor(phase);
            // The finger presses in the first quarter of the cycle; the ring leaves as it lifts
            var press = phase < 0.25f ? Mathf.Sin(phase / 0.25f * Mathf.PI) : 0f;
            var tap = 1f - TapDepth * press;
            dot.localScale = new Vector3(tap, tap, 1f);
            var t = Mathf.Clamp01((phase - 0.12f) / 0.88f);
            var grow = 1f + RingGrow * (1f - (1f - t) * (1f - t));
            ring.localScale = new Vector3(grow, grow, 1f);
            ringGroup.alpha = 0.6f * (1f - t);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
    }
}
