using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // The game world's backdrop, the menu's background behind the scene: a quad that fills the camera's view near its far
    // plane, drawn from vertex colours on the ParticlesUnlit material (no texture of its own). Solid: Background;
    // Gradient: Background tinted towards Primary at the top and Accent at the bottom; Pattern: that gradient under
    // faint Primary diamonds (KitBackground.Colors, so the world and the screens match). A child of GameRig's camera;
    // refits when the camera's aspect, field of view or size changes. UiBinder binds it (a KitElement).
    // With the skin's game background (GameSkin.GameBackground) a child Picture sprite, a little farther away and
    // sorted under the quad, covers the view in its place, and the quad turns into one rectangle in the skin's scrim
    // (clear without one).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ThemeBackground : KitElement
    {
        // Share of the camera's far plane where the quad sits, and its overscan
        public const float Depth = 0.9f;
        private const float Overscan = 1.05f;
        // The picture's depth as a share of the quad's, so it sits behind it
        private const float PictureDepth = 1.02f;
        // Under every sprite and particle (2D sorts by order, 3D by distance: it is the farthest thing)
        public const int SortingOrder = -1000;
        // The pattern's cell and diamond, as shares of the view's height (KitBackdrop's on the 1920-unit canvas)
        private const float CellShare = KitBackdrop.Cell / 1920f;
        private const float DiamondShare = KitBackdrop.Diamond / 1920f;
        // The centre of ParticlesUnlit's soft dot, opaque white
        private static readonly Vector2 Opaque = new Vector2(0.5f, 0.5f);

        [SerializeField] private Camera view;

        private Mesh mesh;
        private SpriteRenderer picture;
        private BackgroundStyle look;
        private Color top = Color.white;
        private Color bottom = Color.white;
        private Color pattern = Color.clear;
        private Vector4 fitted;

        public Mesh Mesh => mesh;
        public BackgroundStyle Look => look;
        public Color Top => top;
        public Color Bottom => bottom;
        // The skin's game background; null or inactive without one
        public SpriteRenderer Picture => picture;

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            (look, top, bottom, pattern) = KitBackground.Colors(palette, style);
            Rebuild();
        }

        private void LateUpdate()
        {
            if (view != null && Fit(view) != fitted) Rebuild();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        // What the quad's size depends on
        private static Vector4 Fit(Camera camera) =>
            new Vector4(camera.aspect, camera.orthographic ? -camera.orthographicSize : camera.fieldOfView, camera.farClipPlane, camera.nearClipPlane);

        public void Rebuild()
        {
            if (view == null) view = GetComponentInParent<Camera>();
            if (view == null) return;
            fitted = Fit(view);
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer.sharedMaterial == null) meshRenderer.sharedMaterial = RenderMaterials.Load(RenderMaterials.ParticlesUnlit);
            var skin = GameSkin.Current;
            var background = skin?.GameBackground;
            // Over the picture when there is one
            meshRenderer.sortingOrder = background != null ? SortingOrder + 1 : SortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (mesh == null)
            {
                mesh = new Mesh { name = "ThemeBackground" };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            var depth = Mathf.Max(view.nearClipPlane * 2f, view.farClipPlane * Depth);
            var height = (view.orthographic ? view.orthographicSize * 2f : 2f * depth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Overscan;
            var width = height * Mathf.Max(0.01f, view.aspect);
            var builder = new QuadBuilder(depth);
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var (left, right, down, up) = (-width / 2f, width / 2f, -height / 2f, height / 2f);
            if (picture != null) picture.gameObject.SetActive(background != null);
            if (background != null)
            {
                FitPicture(background, depth * PictureDepth);
                var scrim = Vertex(skin.Scrim ?? Color.clear, linear);
                builder.Add(new Vector2(left, down), new Vector2(right, down), new Vector2(right, up), new Vector2(left, up), scrim, scrim, scrim, scrim);
                builder.Write(mesh, Opaque);
                return;
            }
            var lower = Vertex(bottom, linear);
            var upper = Vertex(top, linear);
            builder.Add(new Vector2(left, down), new Vector2(right, down), new Vector2(right, up), new Vector2(left, up), lower, lower, upper, upper);
            if (look == BackgroundStyle.Pattern && pattern.a > 0f)
            {
                var cell = height / Overscan * CellShare;
                var half = height / Overscan * DiamondShare / 2f;
                var dot = Vertex(pattern, linear);
                var row = 0;
                for (var y = down + cell / 2f; y < up; y += cell, row++)
                {
                    for (var x = left + (row % 2 == 0 ? cell / 2f : cell); x < right; x += cell)
                    {
                        builder.Add(new Vector2(x, y - half), new Vector2(x + half, y), new Vector2(x, y + half), new Vector2(x - half, y), dot, dot, dot, dot);
                    }
                }
            }
            builder.Write(mesh, Opaque);
        }

        // The picture at its depth, uniformly scaled to cover the view's width and height there (with the overscan)
        private void FitPicture(Sprite background, float depth)
        {
            if (picture == null)
            {
                // Made at run time like the mesh, so never saved into the rig; it goes with its parent
                var child = new GameObject("Picture") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(transform, false);
                picture = child.AddComponent<SpriteRenderer>();
            }
            picture.gameObject.SetActive(true);
            picture.sprite = background;
            picture.sortingOrder = SortingOrder;
            picture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            picture.receiveShadows = false;
            var height = (view.orthographic ? view.orthographicSize * 2f : 2f * depth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Overscan;
            var width = height * Mathf.Max(0.01f, view.aspect);
            var size = background.bounds.size;
            var scale = Mathf.Max(width / Mathf.Max(size.x, 1e-4f), height / Mathf.Max(size.y, 1e-4f));
            picture.transform.SetLocalPositionAndRotation(new Vector3(-background.bounds.center.x * scale, -background.bounds.center.y * scale, depth), Quaternion.identity);
            picture.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // Mesh colours reach the shader as they are: in a linear project they must be linear already
        private static Color Vertex(Color color, bool linear) => linear ? color.linear : color;

        private sealed class QuadBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            private readonly float depth;

            public QuadBuilder(float depth) => this.depth = depth;

            // Corners counter-clockwise from bottom left as seen by the camera
            public void Add(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
            {
                var i = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, depth));
                vertices.Add(new Vector3(b.x, b.y, depth));
                vertices.Add(new Vector3(c.x, c.y, depth));
                vertices.Add(new Vector3(d.x, d.y, depth));
                colors.Add(ca);
                colors.Add(cb);
                colors.Add(cc);
                colors.Add(cd);
                // Facing the camera (towards -Z): clockwise as the camera sees it
                triangles.AddRange(new[] { i, i + 3, i + 2, i + 2, i + 1, i });
            }

            public void Write(Mesh mesh, Vector2 uv)
            {
                mesh.Clear();
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                var uvs = new Vector2[vertices.Count];
                for (var i = 0; i < uvs.Length; i++) uvs[i] = uv;
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                // Never culled: the quad always fills the view
                mesh.bounds = new Bounds(new Vector3(0f, 0f, depth), new Vector3(1e5f, 1e5f, 1f));
            }
        }
    }
}
