using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The ads flag: Continue shows a rewarded ad (premium continues free, without one) while the daily rewarded cap
    // allows, and a finished run or level is a break for an interstitial (the pacer decides; GmaAds skips premium).
    // premium may be null (no flag sells premium).
    public sealed class RewardedShellAds : IShellAds
    {
        private readonly IAds ads;
        private readonly IPremiumStatus premium;

        public RewardedShellAds(IAds ads, IPremiumStatus premium)
        {
            this.ads = ads;
            this.premium = premium;
        }

        public bool ContinueIsFree => premium != null && premium.IsPremium;
        public bool RewardedAvailable => ContinueIsFree || ads.RewardedAvailable;

        public async UniTask<bool> ShowRewardedAsync(CancellationToken cancellation)
        {
            if (ContinueIsFree) return true;
            var result = await ads.ShowRewardedAsync(cancellation);
            return result == RewardResult.Rewarded || result == RewardResult.Granted;
        }

        public async UniTask ShowBreakAsync(CancellationToken cancellation) => await ads.ShowInterstitialAsync(cancellation);
    }
}
