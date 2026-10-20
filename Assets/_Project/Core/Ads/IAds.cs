using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Ads
{
    // What the game uses. IsAvailable is false in the editor, until consent allows ads and when it does not; premium
    // players get Skipped interstitials and no banner whatever IsAvailable says, but still the opt-in rewarded ads.
    public interface IAds
    {
        bool IsAvailable { get; }

        // IsAvailable and the daily rewarded cap (AdsConfig.RewardedDailyCap) not reached yet
        bool RewardedAvailable { get; }

        // Pixels the banner covers at the top of the screen, 0 while none is shown; the menu keeps its content below it
        int BannerHeight { get; }

        // After IsAvailable changes and when the daily rewarded cap is reached
        event Action Changed;

        // At a natural break (a finished level); the pacer may skip it
        UniTask<AdResult> ShowInterstitialAsync(CancellationToken cancellation);

        // Give the reward for Rewarded and Granted only; NotReady once the daily cap is reached
        UniTask<RewardResult> ShowRewardedAsync(CancellationToken cancellation);

        // An adaptive banner at the top of the screen; asking while ads are off shows it once they are on
        void ShowBanner();
        void HideBanner();
    }
}
