using System;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Ads
{
    // Google Mobile Ads and UMP behind plain types (GmaGateway is the only class that sees the SDKs). Every answer
    // arrives on Unity's main thread; nothing throws. UMP calls return null when they worked, else the error message.
    public interface IGmaGateway
    {
        UniTask<string> RequestConsentInfoAsync(bool debugEea);
        UniTask<string> ShowConsentFormIfRequiredAsync();
        UniTask<string> ShowPrivacyOptionsAsync();
        ConsentState Consent { get; }
        bool CanRequestAds { get; }
        bool PrivacyOptionsRequired { get; }

        UniTask InitializeAsync();

        // true when an ad is loaded and kept for the next Show
        UniTask<bool> LoadInterstitialAsync(string adUnitId);

        // Shown or Failed; the loaded ad is used up either way
        UniTask<AdResult> ShowInterstitialAsync();

        UniTask<bool> LoadRewardedAsync(string adUnitId);

        // Rewarded, Dismissed or Failed
        UniTask<RewardResult> ShowRewardedAsync();

        // An adaptive banner on the given edge; loaded: true once the first ad is in, false when it could not load
        void ShowBanner(string adUnitId, BannerPosition position, Action<bool> loaded);

        // Pixels of the loaded banner, 0 while none is loaded
        int BannerHeight { get; }
        void HideBanner();
    }
}
