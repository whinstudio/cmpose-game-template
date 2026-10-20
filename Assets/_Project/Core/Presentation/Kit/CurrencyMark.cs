using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // How a game's currency is drawn: its white glyph (null: a disc with Letter) in its own colour, the same in every
    // theme (FallbackIcon.ShowCurrency, KitGlyph)
    public sealed class CurrencyMark
    {
        public CurrencyMark(Sprite icon, Color colour, string letter)
        {
            Icon = icon;
            Colour = colour;
            Letter = string.IsNullOrEmpty(letter) ? "?" : letter;
        }

        public Sprite Icon { get; }
        public Color Colour { get; }
        public string Letter { get; }

        // Black on a light colour, white on a dark one: whichever contrasts more (WCAG relative luminance)
        public Color LetterColour => Luminance(Colour) > 0.179f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;

        public static float Luminance(Color colour)
        {
            var linear = colour.linear;
            return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
        }
    }
}
