using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The kit's 9-slice sprites (Resources/Kit, written by the maintainer's KitSpriteBuilder): Rounded, a white rounded
    // square with RoundedRadius px corners; Shadow, a soft blurred one (ShadowRadius px corners, ShadowBlur px of
    // falloff). Both 100 px per unit, so on the 1080-wide canvas a corner of r units needs a pixelsPerUnitMultiplier
    // of RoundedRadius / r. Star, a chunky five-pointed star with round tips (simple, not sliced: the level card's
    // stars, the menu's sparkles); Gear and Info, the menu's Settings and About icons (simple).
    public static class KitSprites
    {
        public const string RoundedPath = "Kit/Rounded";
        public const string ShadowPath = "Kit/Shadow";
        public const string StarPath = "Kit/Star";
        public const string GearPath = "Kit/Gear";
        public const string InfoPath = "Kit/Info";
        public const float RoundedRadius = 64f;
        public const float ShadowRadius = 32f;
        public const float ShadowBlur = 64f;
        // A radius larger than any box: the slice borders shrink to fit, so the shape is a pill (a circle when square)
        public const float Pill = 2000f;

        private static Sprite rounded;
        private static Sprite shadow;
        private static Sprite star;
        private static Sprite gear;
        private static Sprite info;

        public static Sprite Rounded => rounded != null ? rounded : rounded = Resources.Load<Sprite>(RoundedPath);
        public static Sprite Shadow => shadow != null ? shadow : shadow = Resources.Load<Sprite>(ShadowPath);
        public static Sprite Star => star != null ? star : star = Resources.Load<Sprite>(StarPath);
        public static Sprite Gear => gear != null ? gear : gear = Resources.Load<Sprite>(GearPath);
        public static Sprite Info => info != null ? info : info = Resources.Load<Sprite>(InfoPath);
    }
}
