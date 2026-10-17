using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Slides and fades a screen or panel in and out on unscaled time (the CanvasGroup on the same object): Show
    // activates it and plays it in from offset (units from its rest position), Hide plays it out and deactivates it.
    // With enterOnStart a screen that starts shown (the menu) plays itself in when its scene starts.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenTransition : KitElement
    {
        [SerializeField] private Vector2 offset = new Vector2(0f, -60f);
        [SerializeField] private bool enterOnStart;
        private CanvasGroup group;
        private RectTransform rect;
        private KitTween progress;
        private Vector2 rest;
        private bool restKnown;

        public bool IsShown { get; private set; }
        public bool Animating => progress.Running;
        public float Progress => progress.Value;
        public Vector2 Offset => offset;
        public bool EnterOnStart => enterOnStart;

        public event Action Hidden;

        public void SetOffset(Vector2 value) => offset = value;

        public void SetEnterOnStart(bool value) => enterOnStart = value;

        private void Start()
        {
            if (enterOnStart && !IsShown) Play(true, true);
        }

        public void Show() => Play(true, true);

        public void Hide() => Play(false, true);

        public void Play(bool show, bool animate)
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
                rect = transform as RectTransform;
            }
            if (!restKnown && rect != null)
            {
                rest = rect.anchoredPosition;
                restKnown = true;
            }
            if (show) gameObject.SetActive(true);
            IsShown = show;
            var timings = Timings;
            if (animate) progress.Start(Mathf.Clamp01(progress.Value), show ? 1f : 0f, timings.Transition, show ? timings.Enter : Ease.OutCubic);
            else progress.Snap(show ? 1f : 0f);
            Place();
            if (!progress.Running) Finish();
        }

        private void Update()
        {
            if (!progress.Step(Time.unscaledDeltaTime)) return;
            Place();
            if (!progress.Running) Finish();
        }

        private void Place()
        {
            var p = progress.Value;
            group.alpha = Mathf.Clamp01(p);
            group.interactable = IsShown;
            group.blocksRaycasts = IsShown;
            if (rect != null) rect.anchoredPosition = rest + offset * (1f - p);
        }

        private void Finish()
        {
            if (IsShown) return;
            Hidden?.Invoke();
            gameObject.SetActive(false);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
        }
    }
}
