using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // The wheel's colours from the theme: slices alternate Primary and Accent (SurfaceVariant breaks an odd count's
    // last slice), the rim is Primary 35 % darker; a slice's label reads in OnPrimary, OnAccent or Text
    [RequireComponent(typeof(WheelDisc))]
    public sealed class WheelPaint : KitElement
    {
        public const float RimShade = 0.35f;

        private static readonly ThemeRole[] SliceRoles = { ThemeRole.Primary, ThemeRole.Accent, ThemeRole.SurfaceVariant };
        private static readonly ThemeRole[] LabelRoles = { ThemeRole.OnPrimary, ThemeRole.OnAccent, ThemeRole.Text };

        public static ThemeRole SliceRole(int i, int count) => SliceRoles[WheelDisc.ColourIndex(i, count)];

        public static ThemeRole LabelRole(int i, int count) => LabelRoles[WheelDisc.ColourIndex(i, count)];

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            var colors = new Color[SliceRoles.Length];
            for (var i = 0; i < SliceRoles.Length; i++) colors[i] = palette.Get(SliceRoles[i]);
            GetComponent<WheelDisc>().Paint(colors, ThemedGraphic.Tone(palette.Get(ThemeRole.Primary), RimShade, 1f));
        }
    }
}
