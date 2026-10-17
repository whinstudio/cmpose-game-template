using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // The screen fill KitBackground paints, drawn from vertices (no texture, no material): one colour, a vertical
    // gradient, or the gradient under a grid of small diamonds
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KitBackdrop : MaskableGraphic
    {
        // Pattern cell and diamond size, units; every other row is offset by half a cell
        public const float Cell = 96f;
        public const float Diamond = 14f;

        [SerializeField] private BackgroundStyle look;
        [SerializeField] private Color top = Color.white;
        [SerializeField] private Color bottom = Color.white;
        [SerializeField] private Color pattern = Color.clear;

        public BackgroundStyle Look => look;
        public Color Top => top;
        public Color Bottom => bottom;
        public Color Pattern => pattern;

        // As uGUI's Image: the VertexHelper mesh path, not the legacy one
        private KitBackdrop() => useLegacyMeshGeneration = false;

        public void Paint(BackgroundStyle value, Color topColor, Color bottomColor, Color patternColor)
        {
            look = value;
            top = topColor;
            bottom = bottomColor;
            pattern = patternColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var upper = look == BackgroundStyle.Solid ? bottom : top;
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
                bottom, bottom, upper, upper);
            if (look != BackgroundStyle.Pattern || pattern.a <= 0f) return;
            var half = Diamond / 2f;
            var row = 0;
            for (var y = r.yMin + Cell / 2f; y < r.yMax; y += Cell, row++)
            {
                var start = r.xMin + (row % 2 == 0 ? Cell / 2f : Cell);
                for (var x = start; x < r.xMax; x += Cell)
                {
                    Quad(vh, new Vector2(x, y - half), new Vector2(x + half, y), new Vector2(x, y + half), new Vector2(x - half, y),
                        pattern, pattern, pattern, pattern);
                }
            }
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            var i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero);
            vh.AddVert(b, cb, Vector2.zero);
            vh.AddVert(c, cc, Vector2.zero);
            vh.AddVert(d, cd, Vector2.zero);
            // Clockwise, as uGUI's own quads
            vh.AddTriangle(i, i + 3, i + 2);
            vh.AddTriangle(i + 2, i + 1, i);
        }
    }
}
