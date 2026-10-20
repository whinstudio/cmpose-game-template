using System;

namespace Kturker.UnityBase.Core.Ads
{
    // Ad unit IDs per platform, the interstitial pacing, the daily rewarded cap and the iOS tracking prompt. The generator writes the six ID
    // lines (one const string each, the ID in double quotes); the App IDs live in
    // Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset. The template ships Google's test IDs: they show
    // test ads in every build, release too (with a warning in the log). Replace both before you ship.
    public static class AdsConfig
    {
        public const string BannerAndroid = "ca-app-pub-3940256099942544/9214589741";
        public const string InterstitialAndroid = "ca-app-pub-3940256099942544/1033173712";
        public const string RewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const string BannerIos = "ca-app-pub-3940256099942544/2435281174";
        public const string InterstitialIos = "ca-app-pub-3940256099942544/4411468910";
        public const string RewardedIos = "ca-app-pub-3940256099942544/1712485313";
        public const string TrackingUsageDescription = "This identifier will be used to deliver personalized ads to you.";
        public const int InterstitialStartDelaySeconds = 60;
        public const int InterstitialGapSeconds = 60;
        public const int InterstitialEvery = 2;
        // Rewarded ads watched to the end per local day, all of them together (Continue, the economy's ad rows and spins)
        public const int RewardedDailyCap = 15;

        // Development builds only: UMP treats the device as in the EEA, so its consent form can be tested
        public static readonly bool DebugGeographyEea = false;

        private const string GoogleTestPublisher = "ca-app-pub-3940256099942544/";

        public static AdUnits Units(bool ios) => ios
            ? new AdUnits(BannerIos, InterstitialIos, RewardedIos)
            : new AdUnits(BannerAndroid, InterstitialAndroid, RewardedAndroid);

        public static bool IsTestId(string id) => id != null && id.StartsWith(GoogleTestPublisher, StringComparison.Ordinal);

        public static bool UsesTestIds(AdUnits units) =>
            IsTestId(units.Banner) || IsTestId(units.Interstitial) || IsTestId(units.Rewarded);
    }
}
