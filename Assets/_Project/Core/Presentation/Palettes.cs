using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Core.Presentation
{
    public static class Palettes
    {
        public const string Light = "Theme/Light";

        // The palette dressed with the game's skin (GameSkin.Current), so every theme source hands it out
        public static Palette Load(string resourcesPath)
        {
            var palette = Resources.Load<Palette>(resourcesPath);
            if (palette == null) throw new InvalidOperationException($"Resources/{resourcesPath}.asset is missing");
            return Dress(palette, GameSkin.Current);
        }

        // The skin's role colours over the palette's, light and dark alike: a runtime copy, the asset is never changed.
        // The palette itself without a skin or a skin palette.
        public static Palette Dress(Palette palette, GameSkin skin)
        {
            if (skin == null || skin.Palette.Count == 0) return palette;
            var dressed = Object.Instantiate(palette);
            dressed.name = palette.name + " (skin)";
            foreach (var role in skin.Palette) dressed.Set(role.Key, role.Value);
            return dressed;
        }
    }
}
