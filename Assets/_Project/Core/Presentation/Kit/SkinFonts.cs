using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The fonts a game skin can name by id (GameSkin.json "fonts"): static TMP SDF assets under
    // Fonts/Resources/SkinFonts/, so every build carries them and a game loads one at run time. An id this template
    // does not know (a newer Compass) loads nothing and the theme's fonts stay.
    public static class SkinFonts
    {
        public const string PixelifySans = "pixelify-sans";

        private static readonly Dictionary<string, string> Paths = new Dictionary<string, string>
        {
            [PixelifySans] = "SkinFonts/PixelifySans/PixelifySans-Regular SDF"
        };

        public static IReadOnlyCollection<string> Ids => Paths.Keys;

        // The Resources path (no extension) of a font id; null when the id is unknown
        public static string ResourcePath(string id) => id != null && Paths.TryGetValue(id, out var path) ? path : null;

        // Null when the id is unknown or its asset is absent (a game that deleted the font's folder)
        public static TMP_FontAsset Load(string id)
        {
            var path = ResourcePath(id);
            return path == null ? null : Resources.Load<TMP_FontAsset>(path);
        }
    }
}
