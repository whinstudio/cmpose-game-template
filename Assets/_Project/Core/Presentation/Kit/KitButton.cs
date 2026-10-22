using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A themed button (KitFactory.Button builds it): rounded to the style's radius, standing on the style's surface,
    // coloured by its variant. The Button, PressFeedback and the Label child sit on the same object. An icon button
    // (KitFactory.Button with a sprite) shows the glyph in a faint disc at its left, the label beside it. A game skin
    // (GameSkin) draws the body with its sprite instead, the pressed sprite while the finger is down; while the normal
    // sprite shows, the label and the glyph sit on its raised face (ContentOffset), not on the middle of the whole sprite.
    [RequireComponent(typeof(Button))]
    public sealed class KitButton : KitElement, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private KitButtonVariant variant;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;
        [SerializeField] private Image shine;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image iconBack;
        [SerializeField] private Image icon;
        // How far the label and the glyph are moved off their places now (units); kept with them when a scene is saved
        [SerializeField] private Vector2 contentOffset;
        // The skin entry the body is dressed with (null: the kit's own shape)
        private SkinButton skin;

        // The disc behind an icon: the glyph's colour, faint
        public const float IconBackOpacity = 0.14f;

        public KitButtonVariant Variant => variant;
        public Image Body => body;
        public TMP_Text Label => label;
        // The glyph of an icon button (null without one)
        public Image Icon => icon;
        public Button Button => GetComponent<Button>();
        // The body's corner radius in units
        public float Radius => KitShape.RadiusOf(body);
        // Where the label and the glyph stand off the body's middle: up by half the skin's depth (SkinButton.Depth) while
        // the normal sprite shows; zero while pressed and without a skin
        public Vector2 ContentOffset => contentOffset;

        internal void Init(KitButtonVariant value, Image shadowImage, Image edgeImage, Image bodyImage, Image shineImage, TMP_Text labelText)
        {
            variant = value;
            shadow = shadowImage;
            edge = edgeImage;
            body = bodyImage;
            shine = shineImage;
            label = labelText;
        }

        internal void InitIcon(Image backImage, Image glyphImage)
        {
            iconBack = backImage;
            icon = glyphImage;
        }

        public void SetVariant(KitButtonVariant value)
        {
            variant = value;
            if (Theme != null && Theme.Current != null) Apply(Theme.Current, Theme.Style);
        }

        // Icon is always round; Play is rounder than the style's boxes
        public static float RadiusFor(KitButtonVariant variant, float styleRadius)
        {
            switch (variant)
            {
                case KitButtonVariant.Icon: return KitSprites.Pill;
                case KitButtonVariant.Play: return styleRadius * 1.6f;
                default: return styleRadius;
            }
        }

        public static ThemeRole BodyRole(KitButtonVariant variant)
        {
            switch (variant)
            {
                case KitButtonVariant.Secondary: return ThemeRole.Surface;
                case KitButtonVariant.Play: return ThemeRole.Accent;
                default: return ThemeRole.Primary;
            }
        }

        // The glyph's colour: the Secondary button's in Primary (a large shape: Primary on Surface reads at 3:1 in every
        // family, ThemeFamilyTests), the others' in their label colour
        public static ThemeRole IconRole(KitButtonVariant variant) =>
            variant == KitButtonVariant.Secondary ? ThemeRole.Primary : LabelRole(variant);

        public static ThemeRole LabelRole(KitButtonVariant variant)
        {
            switch (variant)
            {
                case KitButtonVariant.Secondary: return ThemeRole.Text;
                case KitButtonVariant.Play: return ThemeRole.OnAccent;
                default: return ThemeRole.OnPrimary;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (skin == null || body == null || !Button.IsInteractable()) return;
            body.sprite = skin.Pressed;
            Offset(Vector2.zero);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (skin == null || body == null) return;
            body.sprite = skin.Normal;
            Offset(FaceOffset());
        }

        // Half the depth of the skin's normal sprite in units: depth source pixels drawn at the skin's SliceScale
        private Vector2 FaceOffset()
        {
            var current = GameSkin.Current;
            if (skin == null || skin.Normal == null || current == null) return Vector2.zero;
            var canvas = GetComponentInParent<Canvas>(true);
            var reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            return new Vector2(0f, skin.Depth * current.SliceScale * reference / skin.Normal.pixelsPerUnit / 2f);
        }

        // Moves the label and the glyph (the disc with its glyph, if there is one) to value off their places
        private void Offset(Vector2 value)
        {
            var delta = value - contentOffset;
            if (delta == Vector2.zero) return;
            contentOffset = value;
            if (label != null) label.rectTransform.anchoredPosition += delta;
            var glyph = iconBack != null ? iconBack : icon;
            if (glyph != null) glyph.rectTransform.anchoredPosition += delta;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            var current = GameSkin.Current;
            skin = current?.Button(variant);
            if (skin != null)
            {
                Dress(current);
                return;
            }
            Offset(Vector2.zero);
            var radius = RadiusFor(variant, style != null ? style.CornerRadius : 0f);
            ThemeRole? outline = variant == KitButtonVariant.Secondary ? ThemeRole.Control : (ThemeRole?)null;
            KitShape.Layers(shadow, edge, body, shine, radius, style, BodyRole(variant), outline);
            if (label != null)
            {
                var themed = Themed(label);
                themed.SetFixed(null);
                if (themed.Role != LabelRole(variant)) themed.SetRole(LabelRole(variant));
            }
            if (icon == null) return;
            var role = IconRole(variant);
            var glyph = Themed(icon);
            glyph.SetFixed(null);
            if (glyph.Role != role) glyph.SetRole(role);
            if (iconBack == null) return;
            var back = Themed(iconBack);
            back.SetFixed(null);
            if (back.Role != role) back.SetRole(role);
            back.SetTone(0f, IconBackOpacity);
        }

        // The skin's sprite in its own colours; the label and the glyph in the skin's text colour
        private void Dress(GameSkin current)
        {
            KitShape.Dress(shadow, edge, body, shine, skin.Normal, current.SliceScale, current.Pixel);
            if (label != null) KitShape.Fix(label, skin.Text);
            if (icon != null) KitShape.Fix(icon, skin.Text);
            if (iconBack != null) KitShape.Fix(iconBack, ThemedGraphic.Tone(skin.Text, 0f, IconBackOpacity));
            Offset(FaceOffset());
        }
    }
}
