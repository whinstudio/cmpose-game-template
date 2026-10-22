using System;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The level card's stars: each slot an empty star (Track) under a filled one (Accent) that springs in, one after the
    // other, on unscaled time (the card may open while paused). A game skin's star (GameSkin.Star) draws the slots in its
    // empty sprite and the fills in its full one, both in their own colours.
    public sealed class StarRow : KitElement
    {
        private const float FirstDelay = 0.3f;
        private const float Gap = 0.32f;
        private const float Pop = 0.45f;

        [SerializeField] private Image[] slots;
        [SerializeField] private Image[] fills;
        private int count;
        private int landed;
        private float elapsed = -1f;

        public bool Playing => elapsed >= 0f;
        public int Landed => landed;

        // Each star as it lands, then once after the last one (at once for none)
        public event Action Filled;
        public event Action Finished;

        public void Play(int stars)
        {
            count = Mathf.Clamp(stars, 0, fills.Length);
            landed = 0;
            foreach (var fill in fills)
            {
                fill.rectTransform.localScale = Vector3.zero;
                fill.gameObject.SetActive(false);
            }
            if (count == 0)
            {
                elapsed = -1f;
                Finished?.Invoke();
                return;
            }
            elapsed = 0f;
        }

        // The end state at once (screenshots)
        public void Snap(int stars)
        {
            count = Mathf.Clamp(stars, 0, fills.Length);
            landed = count;
            elapsed = -1f;
            for (var i = 0; i < fills.Length; i++)
            {
                fills[i].gameObject.SetActive(i < count);
                fills[i].rectTransform.localScale = Vector3.one;
            }
        }

        // Steps the stars by dt seconds (Update calls it with the unscaled delta)
        public void Advance(float dt)
        {
            if (elapsed < 0f) return;
            elapsed += dt;
            for (var i = 0; i < count; i++)
            {
                var t = (elapsed - FirstDelay - i * Gap) / Pop;
                if (t < 0f) continue;
                fills[i].gameObject.SetActive(true);
                var value = UiMotion.Evaluate(Ease.Spring, t);
                fills[i].rectTransform.localScale = new Vector3(value, value, 1f);
                if (t < 1f || i < landed) continue;
                landed = i + 1;
                Filled?.Invoke();
            }
            if (landed < count) return;
            elapsed = -1f;
            Finished?.Invoke();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            if (Playing) Snap(count);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            var skin = GameSkin.Current?.Star;
            foreach (var slot in slots) Star(slot, skin?.Empty);
            foreach (var fill in fills) Star(fill, skin?.Full);
        }

        // The skin's sprite in its own colours, or (none) the kit's star in the image's role
        private static void Star(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite != null ? sprite : KitSprites.Star;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            KitShape.Fix(image, sprite != null ? Color.white : (Color?)null);
        }
    }
}
