using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A white glyph in a colour of its own instead of its role's (the economy's currency: gold, blue, orange whatever
    // the family), with a thin rim of the same colour RimShade darker so it reads on a light surface and on an Accent
    // tile alike. Null gives the glyph back to its role and drops the rim.
    public static class KitGlyph
    {
        public const float RimShade = 0.5f;
        // The rim's offset in canvas units (Outline draws four copies this far out)
        public static readonly Vector2 RimDistance = new Vector2(3f, -3f);

        public static void Paint(Graphic glyph, Color? colour)
        {
            var themed = glyph.GetComponent<ThemedGraphic>();
            if (themed != null) themed.SetFixed(colour);
            // Also before a theme is bound (the themed graphic paints only once bound)
            if (colour.HasValue) glyph.color = colour.Value;
            var rim = glyph.GetComponent<Outline>();
            if (!colour.HasValue)
            {
                if (rim != null) rim.enabled = false;
                return;
            }
            if (rim == null) rim = glyph.gameObject.AddComponent<Outline>();
            rim.effectColor = Rim(colour.Value);
            rim.effectDistance = RimDistance;
            rim.useGraphicAlpha = true;
            rim.enabled = true;
        }

        public static Color Rim(Color colour) => ThemedGraphic.Tone(colour, RimShade, 1f);
    }
}
