using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The game's own pictures, written by Compass to Resources/Branding: the title logo (the menu and the boot screen
    // show it instead of the title text) and the menu background (a still of the game's world, drawn over the theme's
    // background instead of the decor shapes). Either may be absent: the screens then look as they always did.
    public static class GameBranding
    {
        public const string TitleLogoPath = "Branding/title-logo";
        public const string MenuBackgroundPath = "Branding/menu-background";

        private static Sprite titleLogo;
        private static Sprite menuBackground;
        private static bool loaded;

        // Null without the file; read once
        public static Sprite TitleLogo
        {
            get
            {
                Load();
                return titleLogo;
            }
        }

        // Null without the file; read once
        public static Sprite MenuBackground
        {
            get
            {
                Load();
                return menuBackground;
            }
        }

        // Replaces both pictures (both null: read the files again)
        internal static void ForTests(Sprite logo, Sprite background)
        {
            titleLogo = logo;
            menuBackground = background;
            loaded = logo != null || background != null;
        }

        private static void Load()
        {
            if (loaded) return;
            titleLogo = Resources.Load<Sprite>(TitleLogoPath);
            menuBackground = Resources.Load<Sprite>(MenuBackgroundPath);
            loaded = true;
        }
    }
}
