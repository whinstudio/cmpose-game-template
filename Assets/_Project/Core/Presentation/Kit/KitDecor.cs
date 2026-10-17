using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A decorative shape behind a screen's content (the menu's soft circles, tilted squares and sparkles): a faint
    // role colour that drifts up and down and sways a little, on unscaled time. Not content: it takes no touches and
    // may reach past the screen's sides (UiCoverageTests skips it).
    [RequireComponent(typeof(Image))]
    public sealed class KitDecor : KitElement
    {
        [SerializeField] private KitDecorShape shape;
        [SerializeField] private float drift = 14f;
        [SerializeField] private float sway = 3f;
        // Where in its cycle the shape starts (0..1), so neighbours do not move in step
        [SerializeField, Range(0f, 1f)] private float offset;
        [SerializeField] private float tilt;
        private Vector2 rest;
        private bool running;
        private float phase;

        public KitDecorShape Shape => shape;
        public float Drift => drift;
        // A cycle lasts this many breaths of the style's motion
        public const float Breaths = 2.5f;

        public void Init(KitDecorShape value, float driftUnits, float swayDegrees, float phaseOffset, float tiltDegrees)
        {
            shape = value;
            drift = driftUnits;
            sway = swayDegrees;
            offset = Mathf.Repeat(phaseOffset, 1f);
            tilt = tiltDegrees;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void OnEnable()
        {
            rest = ((RectTransform)transform).anchoredPosition;
            running = true;
            phase = offset;
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // Moves the shape on by seconds of its cycle
        public void Advance(float seconds)
        {
            if (!running) OnEnable();
            phase = Mathf.Repeat(phase + seconds / Mathf.Max(Timings.Breath * Breaths, 0.1f), 1f);
            var wave = Mathf.Sin(2f * Mathf.PI * phase);
            ((RectTransform)transform).anchoredPosition = rest + new Vector2(0f, drift * wave);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt + sway * Mathf.Cos(2f * Mathf.PI * phase));
        }

        private void OnDisable() => Rest();

        // Back to where it was placed, at its tilt, until it moves again
        public void Rest()
        {
            if (!running) return;
            ((RectTransform)transform).anchoredPosition = rest;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            running = false;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            var image = GetComponent<Image>();
            image.raycastTarget = false;
            switch (shape)
            {
                case KitDecorShape.Circle:
                    KitShape.Round(image, KitSprites.Pill);
                    break;
                case KitDecorShape.Square:
                    // A third of the box: rounder than the style's buttons, so it reads as a shape, not a control
                    KitShape.Round(image, Mathf.Max(style != null ? style.CornerRadius : 0f, ((RectTransform)transform).rect.width / 3f));
                    break;
                default:
                    image.sprite = KitSprites.Star;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    break;
            }
        }
    }
}
