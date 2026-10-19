using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A rounded panel on the style's surface (KitFactory.Card builds it): Surface, or SurfaceVariant when emphasised.
    // Content goes under the card after its Body child. A game skin (GameSkin) draws it with its card sprite, or as a
    // sheet's panel with the sheet's or the dialog's.
    public sealed class KitCard : KitElement
    {
        [SerializeField] private bool emphasis;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;

        public Image Body => body;
        public bool Emphasis => emphasis;
        public float Radius => KitShape.RadiusOf(body);

        internal void Init(bool emphasised, Image shadowImage, Image edgeImage, Image bodyImage)
        {
            emphasis = emphasised;
            shadow = shadowImage;
            edge = edgeImage;
            body = bodyImage;
        }

        public void SetEmphasis(bool value)
        {
            emphasis = value;
            if (Theme != null && Theme.Current != null) Apply(Theme.Current, Theme.Style);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            var skin = GameSkin.Current;
            var sprite = skin != null ? SkinSprite(skin) : null;
            if (sprite != null)
            {
                KitShape.Dress(shadow, edge, body, null, sprite, skin.SliceScale, skin.Pixel);
                return;
            }
            // Panels are a step rounder than the buttons on them
            var radius = (style != null ? style.CornerRadius : 0f) * 1.25f;
            KitShape.Layers(shadow, edge, body, null, radius, style, emphasis ? ThemeRole.SurfaceVariant : ThemeRole.Surface, depthScale: 0.6f);
        }

        private Sprite SkinSprite(GameSkin skin)
        {
            var sheet = transform.parent != null ? transform.parent.GetComponent<KitSheet>() : null;
            return sheet != null && sheet.Panel == transform ? KitSheet.PanelSprite(skin, sheet.Placement) : skin.Card;
        }
    }
}
