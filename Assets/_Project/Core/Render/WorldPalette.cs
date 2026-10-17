using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // The colours game objects draw with: the style's worldColors (three or four, apart from the UI palette; the index
    // wraps) or a palette role's colour, each as a tinted copy of one of the RenderMaterials. Copies are cached per
    // material and colour, so asking every frame is cheap and renderers share them (batching). Glow is tinted
    // GlowIntensity times brighter than the colour, above the bloom threshold of every family.
    //   renderer.sharedMaterial = WorldPalette.Material(2, RenderMaterials.SoftGloss);
    public static class WorldPalette
    {
        public const float GlowIntensity = 1.3f;

        private static readonly Dictionary<(string, Color), Material> Tinted = new Dictionary<(string, Color), Material>();
        private static readonly Color[] Fallback = { new Color(0.23f, 0.51f, 0.96f), new Color(0.96f, 0.62f, 0.04f), new Color(0.06f, 0.73f, 0.51f) };
        private static ThemeStyle style;

        // The style's world colours (the style does not change while the app runs)
        public static IReadOnlyList<Color> Colors
        {
            get
            {
                if (style == null) style = Resources.Load<ThemeStyle>(ThemeStyle.ResourcePath);
                var colors = style != null ? style.WorldColors : null;
                return colors != null && colors.Count > 0 ? colors : Fallback;
            }
        }

        public static int Count => Colors.Count;

        // The index-th world colour; any index wraps (negative too)
        public static Color Color(int index)
        {
            var colors = Colors;
            var n = colors.Count;
            return colors[((index % n) + n) % n];
        }

        // The index-th world colour on the named material (RenderMaterials.Lit, SoftGloss, …)
        public static Material Material(int index, string preset) => Material(Color(index), preset);

        // Any colour (a palette role's: theme.Current.Get(ThemeRole.Primary)) on the named material
        public static Material Material(Color color, string preset)
        {
            if (Tinted.TryGetValue((preset, color), out var material) && material != null) return material;
            material = new Material(RenderMaterials.Load(preset)) { name = $"{preset} #{ColorUtility.ToHtmlStringRGB(color)}" };
            var tint = preset == RenderMaterials.Glow ? new Color(color.r * GlowIntensity, color.g * GlowIntensity, color.b * GlowIntensity, color.a) : color;
            material.SetColor(RenderMaterials.BaseColor, tint);
            Tinted[(preset, color)] = material;
            return material;
        }
    }
}
