using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A rounded panel on the style's surface (KitFactory.Card builds it): Surface, or SurfaceVariant when emphasised.
    // Content goes under the card after its Body child. A game skin (GameSkin) draws it with its card sprite, or as a
    // sheet's panel with the sheet's or the dialog's. A padded card (Pad) keeps its content clear of that sprite's
    // frame: on a side where the frame is thicker than the padding, the content sits FrameGap inside its inner edge.
    public sealed class KitCard : KitElement
    {
        // Units between a skin frame's inner edge and the content (3 source pixels of a pixel pack at sliceScale 4)
        public const float FrameGap = 12f;

        [SerializeField] private bool emphasis;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;
        // Pad's padding (without a skin's frame) and the content it pads besides the card's own LayoutGroup
        [SerializeField] private bool padded;
        [SerializeField] private RectOffset padding = new RectOffset();
        [SerializeField] private RectTransform content;

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

        // The padding Pad was given (zeros for an unpadded card)
        public RectOffset Padding => new RectOffset(padding.left, padding.right, padding.top, padding.bottom);

        // The padding the content gets: Padding, or on each side the skin frame's thickness plus FrameGap where that is
        // more. A frame without a border (or no skin) leaves Padding as it is.
        public RectOffset ContentPadding
        {
            get
            {
                var frame = Frame;
                return new RectOffset(Side(padding.left, frame.x), Side(padding.right, frame.z), Side(padding.top, frame.w),
                    Side(padding.bottom, frame.y));
            }
        }

        // How far the skin's frame reaches into the card on each side, in the card's units (left, bottom, right, top):
        // the sprite's 9-slice border drawn at the skin's SliceScale; zero without a skin
        public Vector4 Frame
        {
            get
            {
                var skin = GameSkin.Current;
                var sprite = skin != null ? SkinSprite(skin) : null;
                if (sprite == null) return Vector4.zero;
                var canvas = GetComponentInParent<Canvas>(true);
                var reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
                return sprite.border * (reference * skin.SliceScale / sprite.pixelsPerUnit);
            }
        }

        // Pads the card's content: the LayoutGroup on the card (a column card) and content (a sheet's Content), if any.
        // Applied at once and again whenever the card is painted. A later call replaces the padding and keeps the content
        // an earlier one gave unless it passes another (the settings column re-pads its sheet's card: the unused Content
        // then follows the column's padding too).
        public void Pad(RectOffset value, RectTransform contentRect = null)
        {
            padded = true;
            padding = new RectOffset(value.left, value.right, value.top, value.bottom);
            if (contentRect != null) content = contentRect;
            Inset();
        }

        // Whole units, rounded up (a hair over a whole number is float noise, not another unit)
        private static int Side(int padding, float frame) =>
            frame > 0f ? Mathf.Max(padding, Mathf.CeilToInt(frame + FrameGap - 0.01f)) : padding;

        private void Inset()
        {
            if (!padded) return;
            var inset = ContentPadding;
            var layout = GetComponent<LayoutGroup>();
            if (layout != null && !Same(layout.padding, inset)) layout.padding = inset;
            if (content == null) return;
            content.offsetMin = new Vector2(inset.left, inset.bottom);
            content.offsetMax = new Vector2(-inset.right, -inset.top);
        }

        private static bool Same(RectOffset a, RectOffset b) =>
            a != null && a.left == b.left && a.right == b.right && a.top == b.top && a.bottom == b.bottom;

        public void SetEmphasis(bool value)
        {
            emphasis = value;
            if (Theme != null && Theme.Current != null) Apply(Theme.Current, Theme.Style);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            Inset();
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
