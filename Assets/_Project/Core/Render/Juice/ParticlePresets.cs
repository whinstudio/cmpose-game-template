using System.Collections.Generic;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // Four ready particle effects on the ParticlesUnlit material, one pooled system each per scene (made on first use,
    // gone with the scene), emitted at a world point: Burst (a pop of round sparks in one colour), Confetti (tumbling
    // flakes in the world colours, falling), Dust (a soft grey puff on the ground) and Sparkle (a few twinkling dots
    // rising). Game time; world simulation space, so one system serves any number of points.
    //   ParticlePresets.Burst(piece.position, WorldPalette.Color(0));
    public static class ParticlePresets
    {
        public enum Preset
        {
            Burst,
            Confetti,
            Dust,
            Sparkle
        }

        private const int SortingOrder = 400;
        private static readonly Dictionary<Preset, ParticleSystem> Systems = new Dictionary<Preset, ParticleSystem>();
        private static Material flakes;

        public static ParticleSystem Burst(Vector3 position, Color color, int count = 24) => Emit(Preset.Burst, position, color, count);

        public static ParticleSystem Confetti(Vector3 position, int count = 60) => Emit(Preset.Confetti, position, Color.white, count);

        public static ParticleSystem Dust(Vector3 position, int count = 10) => Emit(Preset.Dust, position, new Color(1f, 1f, 1f, 0.55f), count);

        public static ParticleSystem Sparkle(Vector3 position, Color color, int count = 12) => Emit(Preset.Sparkle, position, color, count);

        public static ParticleSystem Emit(Preset preset, Vector3 position, Color color, int count)
        {
            var system = SystemFor(preset);
            // The system moves to the point (its particles live in world space, so earlier ones stay where they were)
            system.transform.position = position;
            if (preset != Preset.Confetti)
            {
                var main = system.main;
                main.startColor = color;
            }
            system.Emit(Mathf.Max(0, count));
            return system;
        }

        // The preset's system in the active scene (made on first use)
        public static ParticleSystem SystemFor(Preset preset)
        {
            if (Systems.TryGetValue(preset, out var existing) && existing != null) return existing;
            var go = new GameObject($"Particles{preset}");
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 500;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = RenderMaterials.Load(RenderMaterials.ParticlesUnlit);
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            switch (preset)
            {
                case Preset.Burst:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
                    main.gravityModifier = 0.6f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
                    fade.color = Fade(0.7f);
                    var drag = system.limitVelocityOverLifetime;
                    drag.enabled = true;
                    drag.drag = 4f;
                    break;
                case Preset.Confetti:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
                    main.startSize3D = true;
                    main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(0.2f, 0.32f);
                    main.startSizeZ = 1f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.gravityModifier = 1.1f;
                    main.startColor = WorldColors();
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 30f;
                    shape.radius = 0.2f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.8f));
                    fade.color = Fade(0.8f);
                    var spin = system.rotationOverLifetime;
                    spin.enabled = true;
                    spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                    var flutter = system.limitVelocityOverLifetime;
                    flutter.enabled = true;
                    flutter.drag = 1.2f;
                    renderer.sharedMaterial = Flakes();
                    break;
                case Preset.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    main.gravityModifier = -0.05f;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.3f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
                    fade.color = Fade(0.3f);
                    break;
                default:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                    main.gravityModifier = -0.15f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    var twinkle = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.5f, 0.5f),
                        new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
                    size.size = new ParticleSystem.MinMaxCurve(1f, twinkle);
                    fade.color = Fade(0.75f);
                    break;
            }
            Systems[preset] = system;
            return system;
        }

        // Opaque, then fading out from start (share of the lifetime) to the end
        private static ParticleSystem.MinMaxGradient Fade(float start)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, start), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        // A random pick of the world colours
        private static ParticleSystem.MinMaxGradient WorldColors()
        {
            var count = Mathf.Min(WorldPalette.Count, 8);
            var keys = new GradientColorKey[count];
            for (var i = 0; i < count; i++) keys[i] = new GradientColorKey(WorldPalette.Color(i), (i + 1f) / count);
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        // Confetti are crisp flakes: the particle material without its soft dot
        private static Material Flakes()
        {
            if (flakes != null) return flakes;
            flakes = new Material(RenderMaterials.Load(RenderMaterials.ParticlesUnlit)) { name = "ParticlesUnlit Flakes" };
            flakes.SetTexture(RenderMaterials.BaseMap, Texture2D.whiteTexture);
            return flakes;
        }
    }
}
