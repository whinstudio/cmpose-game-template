using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // "New best": a confetti burst from this object's centre and a scale pop of the target, on unscaled time. The
    // pieces are made once, on the first Play, in the style's world colours and the palette's Accent and Primary;
    // they are runtime-only, never in a scene. The burst pattern is fixed (golden-angle spread), not random.
    public sealed class Celebration : KitElement
    {
        private const int Pieces = 36;
        private const float Life = 1.4f;
        private const float Gravity = -2600f;
        private const float PopAmount = 0.3f;
        private const float PopSeconds = 0.5f;
        private const float GoldenAngle = 2.39996323f;

        [SerializeField] private RectTransform popTarget;
        private RectTransform[] pieces;
        private Image[] images;
        private Vector2[] velocity;
        private float[] spin;
        private float elapsed = -1f;
        private Color[] colours = { Color.white };

        public bool Playing => elapsed >= 0f;
        public RectTransform PopTarget => popTarget;

        internal void Init(RectTransform target) => popTarget = target;

        public void SetPopTarget(RectTransform target) => popTarget = target;

        public void Play()
        {
            Build();
            for (var i = 0; i < Pieces; i++)
            {
                // Upward fan: angles spread by the golden angle over 140°, speeds and spins varied by the index
                var spread = (i * GoldenAngle % (2f * Mathf.PI)) / (2f * Mathf.PI);
                var angle = Mathf.Deg2Rad * (20f + 140f * spread);
                var speed = 1100f + 700f * ((i * 7 % 11) / 10f);
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                spin[i] = (i % 2 == 0 ? 1f : -1f) * (240f + 60f * (i % 5));
                pieces[i].anchoredPosition = Vector2.zero;
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, i * 37f);
                images[i].color = colours[i % colours.Length];
                pieces[i].gameObject.SetActive(true);
            }
            elapsed = 0f;
        }

        private void Build()
        {
            if (pieces != null) return;
            pieces = new RectTransform[Pieces];
            images = new Image[Pieces];
            velocity = new Vector2[Pieces];
            spin = new float[Pieces];
            for (var i = 0; i < Pieces; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = i % 3 == 0 ? new Vector2(22f, 22f) : new Vector2(16f, 30f);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                KitShape.Round(image, i % 3 == 0 ? KitSprites.Pill : 4f);
                go.SetActive(false);
                pieces[i] = rect;
                images[i] = image;
            }
        }

        private void Update()
        {
            if (elapsed < 0f) return;
            var dt = Time.unscaledDeltaTime;
            elapsed += dt;
            var fade = Mathf.Clamp01((Life - elapsed) / (Life * 0.3f));
            for (var i = 0; i < Pieces; i++)
            {
                velocity[i].y += Gravity * dt;
                velocity[i].x *= 1f - 1.5f * dt;
                pieces[i].anchoredPosition += velocity[i] * dt;
                pieces[i].Rotate(0f, 0f, spin[i] * dt);
                var colour = images[i].color;
                colour.a = fade;
                images[i].color = colour;
            }
            if (popTarget != null)
            {
                var value = 1f + PopAmount * UiMotion.Punch(Mathf.Clamp01(elapsed / PopSeconds));
                popTarget.localScale = new Vector3(value, value, 1f);
            }
            if (elapsed < Life) return;
            Stop();
        }

        public void Stop()
        {
            elapsed = -1f;
            if (popTarget != null) popTarget.localScale = Vector3.one;
            if (pieces == null) return;
            foreach (var piece in pieces) piece.gameObject.SetActive(false);
        }

        private void OnDisable() => Stop();

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            var world = style != null ? style.WorldColors : null;
            var count = 2 + (world != null ? world.Count : 0);
            colours = new Color[count];
            colours[0] = palette.Get(ThemeRole.Accent);
            colours[1] = palette.Get(ThemeRole.Primary);
            for (var i = 2; i < count; i++) colours[i] = world[i - 2];
        }
    }
}
