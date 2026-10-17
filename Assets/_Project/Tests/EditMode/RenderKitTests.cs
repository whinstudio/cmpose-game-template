using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Render;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The scene presentation kit (Core/Render): the shared materials are assets (no Shader.Find), rounded meshes are
    // soft and face outwards, the world palette has its colours, GameRig carries the lights and the post volume, and
    // the juice helpers end where they started
    public sealed class RenderKitTests
    {
        private const string RigPath = "Assets/_Project/Render/GameRig.prefab";
        private const string RendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        [Test]
        public void MaterialsLoadFromResources()
        {
            Assert.That(RenderMaterials.All, Is.EquivalentTo(new[] { "Lit", "Unlit", "ParticlesUnlit", "Matte", "SoftGloss", "Glow" }));
            foreach (var name in RenderMaterials.All)
            {
                var material = Resources.Load<Material>("Materials/" + name);
                Assert.That(material, Is.Not.Null, $"Resources/Materials/{name}.mat");
                Assert.That(material.shader, Is.Not.Null, name);
                StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name, name);
                Assert.That(material.HasProperty(RenderMaterials.BaseColor), Is.True, $"{name} is tinted through _BaseColor");
                Assert.That(RenderMaterials.Load(name), Is.SameAs(material));
            }
            var particles = RenderMaterials.Load(RenderMaterials.ParticlesUnlit);
            Assert.That(particles.renderQueue, Is.EqualTo((int)RenderQueue.Transparent), "particles blend");
            Assert.That(particles.GetTexture(RenderMaterials.BaseMap), Is.Not.Null, "particles draw the soft dot");
            Assert.That(() => RenderMaterials.Load("Missing"), Throws.ArgumentException);
        }

        // Shaders reach a build only through assets; a run-time lookup returns null on a device (17 Oct finding)
        [Test]
        public void NoShaderFindInProjectCode()
        {
            var call = new Regex(@"Shader\s*\.\s*Find\s*\(");
            var offenders = Directory.GetFiles("Assets/_Project", "*.cs", SearchOption.AllDirectories)
                .Where(path => call.IsMatch(File.ReadAllText(path)))
                .ToList();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void RoundedBoxHasNoSharpCorners()
        {
            var mesh = RoundedMesh.Box(new Vector3(1f, 0.6f, 2f), 0.2f, 4);
            var normals = mesh.normals;
            // A sharp box has only the six face normals; a rounded one turns through the band at every edge
            var tilted = normals.Count(n => Mathf.Abs(n.x) < 0.99f && Mathf.Abs(n.y) < 0.99f && Mathf.Abs(n.z) < 0.99f);
            Assert.That(tilted, Is.GreaterThan(normals.Length / 4), "corner normals");
            var edge = normals.Count(n => Mathf.Abs(n.x) > 0.3f && Mathf.Abs(n.x) < 0.95f && Mathf.Abs(n.z) < 0.01f);
            Assert.That(edge, Is.GreaterThan(0), "normals across an edge band");
            foreach (var n in normals) Assert.That(n.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void RoundedMeshesFaceOutwards()
        {
            AssertOutwards(RoundedMesh.Box(Vector3.one, 0.25f, 3), "box");
            AssertOutwards(RoundedMesh.Box(new Vector3(2f, 0.5f, 1f), 0.25f, 3), "flat box (radius = half its height)");
            AssertOutwards(RoundedMesh.Cylinder(0.5f, 0.3f, 0.08f, 24, 3), "cylinder");
            AssertOutwards(RoundedMesh.Capsule(0.3f, 1.2f, 16, 6), "capsule");
        }

        [Test]
        public void RoundedMeshesAreCachedPerShape()
        {
            Assert.That(RoundedMesh.Box(Vector3.one, 0.1f), Is.SameAs(RoundedMesh.Box(Vector3.one, 0.1f)));
            Assert.That(RoundedMesh.Box(Vector3.one, 0.1f), Is.Not.SameAs(RoundedMesh.Box(Vector3.one, 0.2f)));
            Assert.That(RoundedMesh.Capsule(0.3f, 1f), Is.SameAs(RoundedMesh.Capsule(0.3f, 1f)));
            // An oversized radius is clamped to half the smallest side, never inverted
            var box = RoundedMesh.Box(new Vector3(1f, 0.2f, 1f), 5f);
            Assert.That(box.bounds.size.y, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void WorldPaletteHasAtLeastThreeColours()
        {
            var style = ThemeStyle.Load();
            Assert.That(style.WorldColors.Count, Is.InRange(3, 4));
            Assert.That(WorldPalette.Count, Is.EqualTo(style.WorldColors.Count));
            Assert.That(style.WorldColors.Distinct().Count(), Is.EqualTo(style.WorldColors.Count), "distinct colours");
            Assert.That(WorldPalette.Color(WorldPalette.Count), Is.EqualTo(WorldPalette.Color(0)), "the index wraps");
            Assert.That(WorldPalette.Color(-1), Is.EqualTo(WorldPalette.Color(WorldPalette.Count - 1)), "negative wraps");

            var lit = WorldPalette.Material(1, RenderMaterials.Lit);
            AssertColor(lit.GetColor(RenderMaterials.BaseColor), style.WorldColors[1]);
            Assert.That(lit.shader, Is.SameAs(RenderMaterials.Load(RenderMaterials.Lit).shader));
            Assert.That(WorldPalette.Material(1, RenderMaterials.Lit), Is.SameAs(lit), "cached, so renderers share it");
            AssertColor(RenderMaterials.Load(RenderMaterials.Lit).GetColor(RenderMaterials.BaseColor), Color.white);
            var glow = WorldPalette.Material(0, RenderMaterials.Glow).GetColor(RenderMaterials.BaseColor);
            Assert.That(glow.maxColorComponent, Is.GreaterThan(1f), "glow is brighter than white, so bloom catches it");
        }

        [Test]
        public void RigHasThreeLightsIn3D()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Assert.That(rig, Is.Not.Null);
            var theme = rig.GetComponent<ThemeRig>();
            Assert.That(theme, Is.Not.Null, "ThemeRig on the rig");
            Assert.That(theme.Volume, Is.Not.Null, "post volume");
            Assert.That(theme.Volume.isGlobal, Is.True);
            var camera = rig.GetComponentInChildren<Camera>(true);
            Assert.That(camera.GetComponentInChildren<ThemeBackground>(true), Is.Not.Null, "the theme's backdrop behind the world");
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (renderer.GetType().Name == "Renderer2DData")
            {
                // The 2D render layer: a global 2D light (it lights sprites) and no directional lights
                Assert.That(rig.GetComponentsInChildren<Component>(true).Any(c => c.GetType().Name == "Light2D"), Is.True, "Global Light 2D");
                Assert.That(rig.GetComponentsInChildren<Light>(true), Is.Empty);
                return;
            }
            var lights = rig.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.EqualTo(3), "key, fill, rim");
            Assert.That(lights.All(l => l.type == LightType.Directional), Is.True);
            Assert.That(new[] { theme.Key, theme.Fill, theme.Rim }, Is.EquivalentTo(lights));
            Assert.That(theme.Key.shadows, Is.EqualTo(LightShadows.Soft), "the key light casts soft shadows");
            Assert.That(theme.Fill.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(theme.Rim.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(theme.Key.intensity, Is.GreaterThan(theme.Fill.intensity));
        }

        [Test]
        public void ThemeRigGivesTheVolumeTheStylesPostProfile()
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            made.Add(rig);
            var palette = Resources.Load<Palette>("Theme/Light");
            var style = ThemeStyle.Load();
            var ambient = (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor, RenderSettings.ambientProbe);
            try
            {
                var theme = rig.GetComponent<ThemeRig>();
                theme.Bind(new FixedThemeSource(palette, style));
                Assert.That(theme.Volume.sharedProfile, Is.SameAs(style.Post));
                Assert.That(theme.Volume.enabled, Is.EqualTo(style.Post != null));
                var backdrop = rig.GetComponentInChildren<ThemeBackground>(true);
                var (look, top, bottom, _) = KitBackground.Colors(palette, style);
                Assert.That(backdrop.Look, Is.EqualTo(look));
                Assert.That(backdrop.Top, Is.EqualTo(top));
                Assert.That(backdrop.Bottom, Is.EqualTo(bottom));
                Assert.That(backdrop.Mesh, Is.Not.Null);
                Assert.That(backdrop.Mesh.vertexCount, Is.GreaterThanOrEqualTo(4));
            }
            finally
            {
                (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                    RenderSettings.ambientGroundColor, RenderSettings.ambientProbe) = ambient;
            }
        }

        [Test]
        public void JuiceEndsWhereItStarted()
        {
            var piece = Make("Piece");
            piece.transform.localScale = new Vector3(2f, 1f, 2f);
            var squash = SquashStretch.Play(piece.transform);
            squash.Advance(0.05f);
            Assert.That(piece.transform.localScale, Is.Not.EqualTo(new Vector3(2f, 1f, 2f)), "squashed");
            // A second squash mid-way keeps the original rest scale
            SquashStretch.Play(piece.transform);
            squash.Advance(SquashStretch.DefaultDuration + 0.1f);
            Assert.That(squash.Playing, Is.False);
            Assert.That(piece.transform.localScale, Is.EqualTo(new Vector3(2f, 1f, 2f)));

            var meshRenderer = Make("Flash").AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = RenderMaterials.Load(RenderMaterials.Lit);
            var block = new MaterialPropertyBlock();
            block.SetColor(RenderMaterials.BaseColor, Color.red);
            meshRenderer.SetPropertyBlock(block);
            var flash = HitFlash.Flash(meshRenderer);
            meshRenderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(RenderMaterials.BaseColor), Is.EqualTo(Color.white), "flashes white");
            flash.Advance(HitFlash.DefaultDuration + 0.01f);
            meshRenderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(RenderMaterials.BaseColor), Is.EqualTo(Color.red), "back to its own colour");

            var camera = Make("Camera").AddComponent<Camera>();
            var shake = CameraShake.Shake(0.3f, 0.2f, camera);
            shake.Advance(0.05f);
            Assert.That(shake.Offset.magnitude, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.3f * 1.8f));
            shake.Advance(0.2f);
            Assert.That(shake.Shaking, Is.False);
            shake.Advance(0.01f);
            Assert.That(shake.Offset, Is.EqualTo(Vector3.zero));
            Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero), "the shake never moves the camera itself");
        }

        [Test]
        public void ParticlePresetsAndFloatingTextUseTheKit()
        {
            foreach (ParticlePresets.Preset preset in System.Enum.GetValues(typeof(ParticlePresets.Preset)))
            {
                var system = ParticlePresets.Emit(preset, Vector3.zero, Color.cyan, 5);
                made.Add(system.gameObject);
                // The count is read back after a step (nothing steps a system in the editor by itself)
                system.Simulate(0.01f, true, false);
                Assert.That(system.particleCount, Is.EqualTo(5), preset.ToString());
                Assert.That(system.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                var material = system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material.shader, Is.SameAs(RenderMaterials.Load(RenderMaterials.ParticlesUnlit).shader), preset.ToString());
                Assert.That(ParticlePresets.SystemFor(preset), Is.SameAs(system), "one system per preset");
            }

            var text = FloatingText.Spawn(Vector3.zero, "+10", Color.yellow);
            made.Add(text.gameObject);
            Assert.That(text.Label.text, Is.EqualTo("+10"));
            Assert.That(text.Label.font, Is.SameAs(ThemeStyle.Load().DisplayFont), "the display font");
            text.Advance(FloatingText.Lifetime * 0.5f);
            Assert.That(text.transform.position.y, Is.GreaterThan(0f), "rises");
            text.Advance(FloatingText.Lifetime);
            Assert.That(text.gameObject.activeSelf, Is.False, "back in the pool");
            Assert.That(FloatingText.Spawn(Vector3.zero, "+5", Color.white), Is.SameAs(text), "reused");
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            return go;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(((Vector4)actual - (Vector4)expected).magnitude, Is.LessThan(1e-4f), $"{actual} vs {expected}");
        }

        // Every triangle's winding normal (Unity's front face) agrees with its vertex normals
        private static void AssertOutwards(Mesh mesh, string what)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            var wrong = 0;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (face.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) <= 0f) wrong++;
            }
            Assert.That(wrong, Is.Zero, $"{what}: triangles facing inwards");
        }
    }
}
