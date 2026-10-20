using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // The lucky wheel drawn from vertices (no texture): Slices wedges, slice 0 starting at the top and the rest
    // clockwise, each in its colour, inside a rim. WheelPaint gives the colours from the theme.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WheelDisc : MaskableGraphic
    {
        // Segments of each wedge's arc and the rim's share of the radius
        private const int Segments = 16;
        public const float RimShare = 0.06f;

        [SerializeField] private int slices = 6;
        [SerializeField] private Color[] colors = { Color.white, Color.gray };
        [SerializeField] private Color rim = Color.black;

        public int Slices => slices;

        private WheelDisc() => useLegacyMeshGeneration = false;

        public void SetSlices(int count)
        {
            slices = Mathf.Max(1, count);
            SetVerticesDirty();
        }

        public void Paint(Color[] sliceColors, Color rimColor)
        {
            colors = sliceColors;
            rim = rimColor;
            SetVerticesDirty();
        }

        // The colour of slice i of count (alternating; a third colour keeps an odd count from repeating at slice 0)
        public static int ColourIndex(int i, int count) => count % 2 == 0 ? i % 2 : count > 1 && i == count - 1 ? 2 : i % 2;

        // Degrees clockwise from the top to the middle of slice i
        public static float Middle(int i, int count) => (i + 0.5f) * 360f / Mathf.Max(1, count);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var centre = r.center;
            var outer = Mathf.Min(r.width, r.height) / 2f;
            var inner = outer * (1f - RimShare);
            var step = 360f / slices;
            for (var s = 0; s < slices; s++)
            {
                var colour = colors.Length == 0 ? Color.white : colors[ColourIndex(s, slices) % colors.Length];
                var start = vh.currentVertCount;
                vh.AddVert(centre, colour, Vector2.zero);
                for (var k = 0; k <= Segments; k++) vh.AddVert(centre + Direction(s * step + step * k / Segments) * inner, colour, Vector2.zero);
                for (var k = 0; k < Segments; k++) vh.AddTriangle(start, start + k + 2, start + k + 1);
            }
            var ring = slices * Segments;
            var first = vh.currentVertCount;
            for (var k = 0; k <= ring; k++)
            {
                var direction = Direction(360f * k / ring);
                vh.AddVert(centre + direction * inner, rim, Vector2.zero);
                vh.AddVert(centre + direction * outer, rim, Vector2.zero);
            }
            for (var k = 0; k < ring; k++)
            {
                var a = first + k * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        // Clockwise from the top
        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }
    }
}
