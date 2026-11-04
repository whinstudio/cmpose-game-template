using System;
using UnityEditor;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation.Editor
{
    // Compass writes a still of the game to Resources/Branding/menu-background.png (GameBranding.MenuBackgroundPath) and
    // MenuView shows it behind the menu under the full Scrim. Imported here, it comes out small and blurred
    // (MenuStillBlur), so the game's pieces do not compete with the logo and the buttons; the texture type stays as it
    // is (a Sprite: GameBranding loads it as one). Only that file: the title logo beside it and a skin's designed
    // backgrounds stay sharp. Raise Version when the blur changes, so games already holding a still import it again.
    public sealed class MenuStillImporter : AssetPostprocessor
    {
        public const uint Version = 1;

        // The size cap Unity applies before OnPostprocessTexture: at least this, so a still is never shrunk below
        // the blur's own scale first (the output's long side is far smaller)
        private const int MinMaxTextureSize = 2048;

        private static readonly string StillSuffix = "/" + GameBranding.MenuBackgroundPath + ".png";

        // Runs after the Compass kit's importer, which may point-filter Branding pictures in pixel games
        public const int PostprocessOrder = 100;

        public override uint GetVersion() => Version;

        public override int GetPostprocessOrder() => PostprocessOrder;

        // A png named menu-background in a folder named Branding
        public static bool IsMenuStill(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && assetPath.Replace('\\', '/').EndsWith(StillSuffix, StringComparison.Ordinal);

        private void OnPreprocessTexture()
        {
            if (!IsMenuStill(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (importer.maxTextureSize < MinMaxTextureSize) importer.maxTextureSize = MinMaxTextureSize;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsMenuStill(assetPath)) return;
            var blurred = MenuStillBlur.Blur(texture.GetPixels32(), texture.width, texture.height, out var width, out var height);
            texture.Reinitialize(width, height);
            texture.SetPixels32(blurred);
            texture.Apply(false);
        }
    }
}
