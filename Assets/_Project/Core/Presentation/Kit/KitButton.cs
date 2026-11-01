using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A themed button (KitFactory.Button builds it): rounded to the style's radius, standing on the style's surface,
    // coloured by its variant. The Button, PressFeedback and the Label child sit on the same object. An icon button
    // (KitFactory.Button with a sprite) shows the glyph in a faint disc at its left, the label beside it. A game skin
    // (GameSkin) draws the body with its sprite instead, the pressed sprite while the finger is down; the label and the
    // glyph sit on the middle of the nine-slice centre of the sprite that shows (ContentOffset), not on the middle of the
    // whole sprite. Skinned or not, the label also moves by its font's CapOffset, so its capitals sit on that middle
    // rather than the font's ascender-to-descender box.
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
        // How far the label alone is raised on top of that for its font's capitals now (CapOffset; units)
        [SerializeField] private float capOffset;
        // The skin entry the body is dressed with (null: the kit's own shape)
        private SkinButton skin;
        // The label's ThemedText whose font changes Recap hears
        private ThemedText followed;

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
        // Where the label and the glyph stand off the body's middle: on the middle of the nine-slice centre of the sprite
        // that shows (SkinButton.Lift); zero without a skin
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
            Offset(FaceOffset(skin.Pressed));
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (skin == null || body == null) return;
            body.sprite = skin.Normal;
            Offset(FaceOffset(skin.Normal));
        }

        // How far the label's capitals sit below its middle (units; negative: above) at the size TMP draws it (fontSize:
        // the size autosizing chose once laid out): TMP's Center puts the middle of the font's ascender-to-descender box
        // on the box's middle, the capitals span baseline to cap line
        public static float CapOffset(TMP_Text label)
        {
            var font = label != null ? label.font : null;
            if (font == null) return 0f;
            var face = font.faceInfo;
            if (face.pointSize <= 0) return 0f;
            return ((face.ascentLine + face.descentLine) / 2f - face.capLine / 2f) * label.fontSize / face.pointSize * face.scale;
        }

        // The label's ThemedText may set its font after Apply (bound alone): the capitals follow it
        private void Follow(ThemedText text)
        {
            if (text == followed) return;
            if (followed != null) followed.FontChanged -= Recap;
            followed = text;
            if (followed != null) followed.FontChanged += Recap;
        }

        // TMP chooses an autosized label's size while laying it out: the capitals follow that size once it is known.
        // Moving the label does not lay it out again, so this does not loop
        private void FollowLayout()
        {
            if (label == null) return;
            label.OnPreRenderText -= LaidOut;
            label.OnPreRenderText += LaidOut;
        }

        private void LaidOut(TMP_TextInfo _) => Recap();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (label != null) label.OnPreRenderText -= LaidOut;
            Follow(null);
        }

        // Raises the label by its font's CapOffset, on top of the face's offset
        private void Recap()
        {
            if (!this || label == null) return;
            var value = CapOffset(label);
            var delta = value - capOffset;
            if (delta == 0f) return;
            capOffset = value;
            label.rectTransform.anchoredPosition += new Vector2(0f, delta);
        }

        // The skin's lift of sprite in units: lift source pixels drawn at the skin's SliceScale
        private Vector2 FaceOffset(Sprite sprite)
        {
            var current = GameSkin.Current;
            if (skin == null || sprite == null || current == null) return Vector2.zero;
            var canvas = GetComponentInParent<Canvas>(true);
            var reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            return new Vector2(0f, skin.Lift(sprite) * current.SliceScale * reference / sprite.pixelsPerUnit);
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
            Follow(label != null ? label.GetComponent<ThemedText>() : null);
            FollowLayout();
            Recap();
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
            Offset(FaceOffset(skin.Normal));
        }
    }
}
