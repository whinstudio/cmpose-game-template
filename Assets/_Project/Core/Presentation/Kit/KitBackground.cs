using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Fills a screen the way the style's background asks (KitBackdrop on the same object draws it): Solid, the
    // Background colour; Gradient, Background tinted towards Primary at the top and towards Accent at the bottom;
    // Pattern, that gradient under faint Primary diamonds
    [RequireComponent(typeof(KitBackdrop))]
    public sealed class KitBackground : KitElement
    {
        public const float TopTint = 0.2f;
        public const float BottomTint = 0.08f;
        public const float PatternOpacity = 0.08f;

        public KitBackdrop Backdrop => GetComponent<KitBackdrop>();

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            var (look, top, bottom, pattern) = Colors(palette, style);
            Backdrop.Paint(look, top, bottom, pattern);
        }

        // The look and its colours (top, bottom, pattern; ThemeBackground draws the game world's backdrop with them)
        public static (BackgroundStyle Look, Color Top, Color Bottom, Color Pattern) Colors(Palette palette, ThemeStyle style)
        {
            var look = style != null ? style.Background : BackgroundStyle.Solid;
            var background = palette.Get(ThemeRole.Background);
            if (look == BackgroundStyle.Solid) return (look, background, background, Color.clear);
            var primary = palette.Get(ThemeRole.Primary);
            var top = Color.Lerp(background, primary, TopTint);
            var bottom = Color.Lerp(background, palette.Get(ThemeRole.Accent), BottomTint);
            var dots = look == BackgroundStyle.Pattern ? ThemedGraphic.Tone(primary, 0f, PatternOpacity) : Color.clear;
            return (look, top, bottom, dots);
        }
    }
}
