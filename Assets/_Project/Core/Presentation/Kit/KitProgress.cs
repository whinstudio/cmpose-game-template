using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A progress bar (KitFactory.Progress builds it): a pill Track rail and a Primary pill fill from the left to Value
    // (0..1), hidden at 0 and, while above 0, never narrower than its ends (the sprite's slice borders) so a small value
    // still draws a whole cap. A game skin's progress (GameSkin.Progress) draws the parts it has sprites for in their
    // own colours; a part without one keeps its role and round ends.
    public sealed class KitProgress : KitElement
    {
        [SerializeField] private Image track;
        [SerializeField] private Image fill;
        [SerializeField, Range(0f, 1f)] private float value;

        public Image Track => track;
        public Image Fill => fill;
        public float Value => value;

        internal void Init(Image trackImage, Image fillImage)
        {
            track = trackImage;
            fill = fillImage;
            Place();
        }

        public void SetValue(float target)
        {
            value = Mathf.Clamp01(target);
            Place();
        }

        private void OnRectTransformDimensionsChange() => Place();

        private void Place()
        {
            if (fill == null) return;
            var shown = value > 0f;
            if (fill.gameObject.activeSelf != shown) fill.gameObject.SetActive(shown);
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(value, 1f);
            rect.offsetMin = Vector2.zero;
            var width = ((RectTransform)transform).rect.width * value;
            rect.offsetMax = new Vector2(Mathf.Max(0f, LeastWidth() - width), 0f);
        }

        // The width the fill's slice borders take, units (a pill's: its height, a circle; 0 for a sprite drawn whole)
        private float LeastWidth()
        {
            var pill = fill.GetComponent<KitPill>();
            if (pill != null && pill.enabled) return ((RectTransform)transform).rect.height;
            var sprite = fill.sprite;
            if (sprite == null || fill.type == Image.Type.Simple || fill.type == Image.Type.Filled) return 0f;
            var border = sprite.border;
            var unitsPerPixel = 1f / (fill.pixelsPerUnit * fill.pixelsPerUnitMultiplier);
            return (border.x + border.z) * unitsPerPixel;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (track == null || fill == null) return;
            var skin = GameSkin.Current;
            var parts = skin?.Progress;
            Dress(track, parts?.Track, skin);
            Dress(fill, parts?.Fill, skin);
            Place();
        }

        // The skin's sprite in its own colours, or (none) a pill in the part's role
        private static void Dress(Image image, Sprite sprite, GameSkin skin)
        {
            if (sprite != null) KitShape.DressImage(image, sprite, skin.SliceScale, skin.Pixel);
            else KitShape.UndressImage(image, KitSprites.Pill);
        }
    }
}
