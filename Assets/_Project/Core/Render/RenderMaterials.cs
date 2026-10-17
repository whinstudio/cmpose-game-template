using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // The shared world materials, assets in Core/Presentation/Resources/Materials (the maintainer's RenderKitBuilder
    // writes them), so a build keeps their shaders: never Shader.Find at run time. Lit: URP Lit, a little smooth;
    // Unlit: URP Unlit; ParticlesUnlit: URP Particles/Unlit, alpha-blended, a soft round dot, vertex colours (particles,
    // the theme background); Matte: Lit, no shine; SoftGloss: Lit, glossy; Glow: Unlit, which WorldPalette tints
    // brighter than white so bloom catches it. Every one is tinted through _BaseColor.
    public static class RenderMaterials
    {
        public const string Folder = "Materials/";
        public const string Lit = "Lit";
        public const string Unlit = "Unlit";
        public const string ParticlesUnlit = "ParticlesUnlit";
        public const string Matte = "Matte";
        public const string SoftGloss = "SoftGloss";
        public const string Glow = "Glow";

        public static readonly IReadOnlyList<string> All = new[] { Lit, Unlit, ParticlesUnlit, Matte, SoftGloss, Glow };

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private static readonly Dictionary<string, Material> Loaded = new Dictionary<string, Material>();

        // The shared asset (never modify it: WorldPalette makes tinted copies)
        public static Material Load(string name)
        {
            if (Loaded.TryGetValue(name, out var material) && material != null) return material;
            material = Resources.Load<Material>(Folder + name);
            if (material == null) throw new ArgumentException($"Resources/{Folder}{name}.mat is missing", nameof(name));
            Loaded[name] = material;
            return material;
        }
    }
}
