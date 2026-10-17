using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // A short message on an Accent pill ("Perfect!", "+1"): it pops in, holds, then rises and fades, on unscaled time.
    // The object is off between toasts; a new one restarts it.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ShellToast : MonoBehaviour
    {
        private const float In = 0.22f;
        private const float Hold = 0.6f;
        private const float Out = 0.35f;
        private const float Rise = 60f;
        private const float StartScale = 0.5f;

        [SerializeField] private LocalizedText label;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 rest;
        private bool restKnown;
        private float elapsed = -1f;

        public bool Playing => elapsed >= 0f;

        public void Play(string key)
        {
            Remember();
            label.SetKey(key);
            elapsed = 0f;
            gameObject.SetActive(true);
            Show();
        }

        // Steps the toast by dt seconds (Update calls it with the unscaled delta)
        public void Advance(float dt)
        {
            if (elapsed < 0f) return;
            elapsed += dt;
            if (elapsed >= In + Hold + Out)
            {
                elapsed = -1f;
                rect.anchoredPosition = rest;
                gameObject.SetActive(false);
                return;
            }
            Show();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void Remember()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (rect == null) rect = (RectTransform)transform;
            if (restKnown) return;
            rest = rect.anchoredPosition;
            restKnown = true;
        }

        private void Show()
        {
            var enter = UiMotion.Evaluate(Ease.OutBack, elapsed / In);
            var scale = Mathf.LerpUnclamped(StartScale, 1f, enter);
            rect.localScale = new Vector3(scale, scale, 1f);
            var leave = UiMotion.Evaluate(Ease.OutCubic, (elapsed - In - Hold) / Out);
            group.alpha = Mathf.Clamp01(elapsed / In) * (1f - leave);
            rect.anchoredPosition = rest + new Vector2(0f, Rise * leave);
        }
    }
}
