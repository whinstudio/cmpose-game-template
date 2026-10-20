using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The kit's 9-slice sprites (Resources/Kit, written by the maintainer's KitSpriteBuilder): Rounded, a white rounded
    // square with RoundedRadius px corners; Shadow, a soft blurred one (ShadowRadius px corners, ShadowBlur px of
    // falloff). Both 100 px per unit, so on the 1080-wide canvas a corner of r units needs a pixelsPerUnitMultiplier
    // of RoundedRadius / r. Star, a chunky five-pointed star with round tips (simple, not sliced: the level card's
    // stars, the menu's sparkles); Gear and Info, the Settings and About icons, Podium, the leaderboard's, Video, the
    // rewarded-ad mark, and the economy's Coin and Gem (the currencies' icons; the star currency draws Star), Gift (the
    // supply), Wheel and Calendar (the login bonus) (simple).
    public static class KitSprites
    {
        public const string RoundedPath = "Kit/Rounded";
        public const string ShadowPath = "Kit/Shadow";
        public const string StarPath = "Kit/Star";
        public const string GearPath = "Kit/Gear";
        public const string InfoPath = "Kit/Info";
        public const string PodiumPath = "Kit/Podium";
        public const string VideoPath = "Kit/Video";
        public const string CoinPath = "Kit/Coin";
        public const string GiftPath = "Kit/Gift";
        public const string WheelPath = "Kit/Wheel";
        public const string CalendarPath = "Kit/Calendar";
        public const string GemPath = "Kit/Gem";
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
        private static Sprite podium;
        private static Sprite video;
        private static Sprite coin;
        private static Sprite gift;
        private static Sprite wheel;
        private static Sprite calendar;
        private static Sprite gem;

        public static Sprite Rounded => rounded != null ? rounded : rounded = Resources.Load<Sprite>(RoundedPath);
        public static Sprite Shadow => shadow != null ? shadow : shadow = Resources.Load<Sprite>(ShadowPath);
        public static Sprite Star => star != null ? star : star = Resources.Load<Sprite>(StarPath);
        public static Sprite Gear => gear != null ? gear : gear = Resources.Load<Sprite>(GearPath);
        public static Sprite Info => info != null ? info : info = Resources.Load<Sprite>(InfoPath);
        public static Sprite Podium => podium != null ? podium : podium = Resources.Load<Sprite>(PodiumPath);
        public static Sprite Video => video != null ? video : video = Resources.Load<Sprite>(VideoPath);
        public static Sprite Coin => coin != null ? coin : coin = Resources.Load<Sprite>(CoinPath);
        public static Sprite Gift => gift != null ? gift : gift = Resources.Load<Sprite>(GiftPath);
        public static Sprite Wheel => wheel != null ? wheel : wheel = Resources.Load<Sprite>(WheelPath);
        public static Sprite Calendar => calendar != null ? calendar : calendar = Resources.Load<Sprite>(CalendarPath);
        public static Sprite Gem => gem != null ? gem : gem = Resources.Load<Sprite>(GemPath);
    }
}
