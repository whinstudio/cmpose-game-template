using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A themed button (KitFactory.Button builds it): rounded to the style's radius, standing on the style's surface,
    // coloured by its variant. The Button, PressFeedback and the Label child sit on the same object. An icon button
    // (KitFactory.Button with a sprite) shows the glyph in a faint disc at its left, the label beside it.
    [RequireComponent(typeof(Button))]
    public sealed class KitButton : KitElement
    {
        [SerializeField] private KitButtonVariant variant;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;
        [SerializeField] private Image shine;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image iconBack;
        [SerializeField] private Image icon;

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

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            var radius = RadiusFor(variant, style != null ? style.CornerRadius : 0f);
            ThemeRole? outline = variant == KitButtonVariant.Secondary ? ThemeRole.Control : (ThemeRole?)null;
            KitShape.Layers(shadow, edge, body, shine, radius, style, BodyRole(variant), outline);
            if (label != null)
            {
                var themed = Themed(label);
                if (themed.Role != LabelRole(variant)) themed.SetRole(LabelRole(variant));
            }
            if (icon == null) return;
            var role = IconRole(variant);
            var glyph = Themed(icon);
            if (glyph.Role != role) glyph.SetRole(role);
            if (iconBack == null) return;
            var back = Themed(iconBack);
            if (back.Role != role) back.SetRole(role);
            back.SetTone(0f, IconBackOpacity);
        }
    }
}
