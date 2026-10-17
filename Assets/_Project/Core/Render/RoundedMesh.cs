using System.Collections.Generic;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // Soft-edged meshes for game pieces, built once per shape and cached (meshes are shared, never modify them):
    // a box with rounded edges and corners, a cylinder with rounded rims, a capsule. Centred on the origin, smooth
    // normals, UVs 0..1 per face (box) or around and up (the others).
    //   filter.sharedMesh = RoundedMesh.Box(Vector3.one, 0.15f);
    public static class RoundedMesh
    {
        private static readonly Dictionary<(int, Vector4, int, int), Mesh> Cache = new Dictionary<(int, Vector4, int, int), Mesh>();

        // size: the full box; radius: of its edges, at most half the smallest side; segments: steps per quarter round
        public static Mesh Box(Vector3 size, float radius, int segments = 4)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            radius = Mathf.Clamp(radius, 0.0001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f);
            segments = Mathf.Clamp(segments, 1, 16);
            var key = (0, new Vector4(size.x, size.y, size.z, radius), segments, 0);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var mesh = BuildBox(size, radius, segments);
            Cache[key] = mesh;
            return mesh;
        }

        // A disc-shaped or tall cylinder along Y; bevel: the radius of its top and bottom rims
        public static Mesh Cylinder(float radius, float height, float bevel, int sides = 32, int segments = 4)
        {
            radius = Mathf.Max(0.0001f, Mathf.Abs(radius));
            height = Mathf.Max(0.0001f, Mathf.Abs(height));
            bevel = Mathf.Clamp(bevel, 0.0001f, Mathf.Min(radius, height / 2f));
            sides = Mathf.Clamp(sides, 6, 128);
            segments = Mathf.Clamp(segments, 1, 16);
            var key = (1, new Vector4(radius, height, bevel, 0f), sides, segments);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var profile = new List<(Vector2 Point, Vector2 Normal)> { (new Vector2(0f, -height / 2f), Vector2.down) };
            Arc(profile, new Vector2(radius - bevel, -height / 2f + bevel), bevel, -90f, 0f, segments);
            Arc(profile, new Vector2(radius - bevel, height / 2f - bevel), bevel, 0f, 90f, segments);
            profile.Add((new Vector2(0f, height / 2f), Vector2.up));
            var mesh = Lathe("RoundedCylinder", profile, sides);
            Cache[key] = mesh;
            return mesh;
        }

        // A capsule along Y: height is the whole length (at least the diameter)
        public static Mesh Capsule(float radius, float height, int sides = 24, int segments = 8)
        {
            radius = Mathf.Max(0.0001f, Mathf.Abs(radius));
            height = Mathf.Max(radius * 2f, Mathf.Abs(height));
            sides = Mathf.Clamp(sides, 6, 128);
            segments = Mathf.Clamp(segments, 2, 32);
            var key = (2, new Vector4(radius, height, 0f, 0f), sides, segments);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var profile = new List<(Vector2 Point, Vector2 Normal)>();
            Arc(profile, new Vector2(0f, -height / 2f + radius), radius, -90f, 0f, segments);
            Arc(profile, new Vector2(0f, height / 2f - radius), radius, 0f, 90f, segments);
            var mesh = Lathe("RoundedCapsule", profile, sides);
            Cache[key] = mesh;
            return mesh;
        }

        // Each face is a grid whose lines sit at the inner box's faces and through the rounded band; every grid point is
        // pulled onto the rounded surface (the inner box grown by radius), its normal pointing away from the inner box
        private static Mesh BuildBox(Vector3 size, float radius, int segments)
        {
            var inner = size / 2f - Vector3.one * radius;
            var outer = size / 2f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (var axis = 0; axis < 3; axis++)
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var normal = axes[axis] * sign;
                    var u = axes[(axis + 1) % 3];
                    var v = axes[(axis + 2) % 3];
                    var us = Lines(Vector3.Dot(outer, u), Vector3.Dot(inner, u), segments);
                    var vs = Lines(Vector3.Dot(outer, v), Vector3.Dot(inner, v), segments);
                    var start = vertices.Count;
                    for (var j = 0; j < vs.Length; j++)
                    {
                        for (var i = 0; i < us.Length; i++)
                        {
                            var p = normal * Vector3.Dot(outer, axes[axis]) + u * us[i] + v * vs[j];
                            var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            var n = (p - core).normalized;
                            vertices.Add(core + n * radius);
                            normals.Add(n);
                            uvs.Add(new Vector2(i / (us.Length - 1f), j / (vs.Length - 1f)));
                        }
                    }
                    // Unity's front faces: cross(b - a, c - a) points outwards
                    var along = Vector3.Dot(Vector3.Cross(u, v), normal) > 0f;
                    for (var j = 0; j < vs.Length - 1; j++)
                    {
                        for (var i = 0; i < us.Length - 1; i++)
                        {
                            var a = start + j * us.Length + i;
                            var b = a + 1;
                            var c = a + us.Length + 1;
                            var d = a + us.Length;
                            if (along) triangles.AddRange(new[] { a, b, c, a, c, d });
                            else triangles.AddRange(new[] { a, c, b, a, d, c });
                        }
                    }
                }
            }
            return Finish("RoundedBox", vertices, normals, uvs, triangles);
        }

        // Grid lines across one face: through the rounded band from -outer to -inner, then from inner to outer
        private static float[] Lines(float outer, float inner, int segments)
        {
            var lines = new float[(segments + 1) * 2];
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                lines[i] = Mathf.Lerp(-outer, -inner, t);
                lines[segments + 1 + i] = Mathf.Lerp(inner, outer, t);
            }
            return lines;
        }

        // A quarter (or any) arc of the lathe profile, from degrees to degrees, appended with its normals
        private static void Arc(List<(Vector2 Point, Vector2 Normal)> profile, Vector2 centre, float radius, float from, float to, int segments)
        {
            for (var i = 0; i <= segments; i++)
            {
                var angle = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                profile.Add((centre + normal * radius, normal));
            }
        }

        // Turns the profile (x: distance from the Y axis, y: height; bottom to top) round the Y axis
        private static Mesh Lathe(string name, List<(Vector2 Point, Vector2 Normal)> profile, int sides)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var columns = sides + 1;
            for (var r = 0; r < profile.Count; r++)
            {
                var (point, normal) = profile[r];
                for (var s = 0; s < columns; s++)
                {
                    var angle = (float)s / sides * Mathf.PI * 2f;
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(point.x * cos, point.y, point.x * sin));
                    normals.Add(new Vector3(normal.x * cos, normal.y, normal.x * sin).normalized);
                    uvs.Add(new Vector2((float)s / sides, (float)r / (profile.Count - 1)));
                }
            }
            for (var r = 0; r < profile.Count - 1; r++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var a = r * columns + s;
                    var b = a + 1;
                    var c = a + columns + 1;
                    var d = a + columns;
                    triangles.AddRange(new[] { a, c, b, a, d, c });
                }
            }
            return Finish(name, vertices, normals, uvs, triangles);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
