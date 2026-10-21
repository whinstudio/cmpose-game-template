using System;

namespace Kturker.UnityBase.Core.Ads
{
    // The height Google gives an anchored adaptive banner (AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth),
    // worked out before any ad loads so the screen can keep that room from the start: 50 per 320 wide up to 432, 68 up
    // to 526, 60 per 468 up to 632, 81 up to 655, then 90 per 728; at most 15 % of the screen's height and 90, never
    // under 50. Widths and heights in density-independent units (Android dp, iOS points); rounding as Java's Math.round.
    public static class AnchoredBanner
    {
        public static int HeightDp(int widthDp, float screenHeightDp)
        {
            int height;
            if (widthDp > 655) height = Round(widthDp / 728f * 90f);
            else if (widthDp > 632) height = 81;
            else if (widthDp > 526) height = Round(widthDp / 468f * 60f);
            else if (widthDp > 432) height = 68;
            else height = Round(widthDp / 320f * 50f);
            var cap = Math.Min(90, Round(screenHeightDp * 0.15f));
            return Math.Max(Math.Min(height, cap), 50);
        }

        // 0 when the width or the scale is unknown
        public static int HeightPixels(int widthDp, float screenHeightDp, float scale) =>
            widthDp <= 0 || scale <= 0f ? 0 : Round(HeightDp(widthDp, screenHeightDp) * scale);

        private static int Round(float value) => (int)Math.Floor(value + 0.5f);
    }
}
