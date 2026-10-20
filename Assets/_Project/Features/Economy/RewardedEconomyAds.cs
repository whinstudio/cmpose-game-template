using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Features.Economy
{
    // ads flag: a rewarded ad unlocks the economy's rewards only when it was shown and watched to the end (Rewarded):
    // Granted (a reward without an ad) unlocks nothing here. Premium players watch these too. None once the daily
    // rewarded cap is reached, nor during an AutoPlay run. A held banner (a menu window is open) is hidden.
    public sealed class RewardedEconomyAds : IEconomyAds
    {
        private readonly IAds ads;
        private readonly ILog log;
        private readonly Func<bool> autoPlay;

        public RewardedEconomyAds(IAds ads, ILog log, Func<bool> autoPlay)
        {
            this.ads = ads;
            this.log = log;
            this.autoPlay = autoPlay;
        }

        public bool RewardedAvailable => ads.RewardedAvailable && !autoPlay();

        public async UniTask<bool> ShowRewardedAsync(CancellationToken ct)
        {
            if (!RewardedAvailable) return false;
            RewardResult result;
            try
            {
                result = await ads.ShowRewardedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                log.Error("economy: the rewarded ad failed", e);
                return false;
            }
            return result == RewardResult.Rewarded;
        }

        // The windows open only over the menu, so a released banner is the menu's again
        public void HoldBanner(bool held)
        {
            if (held) ads.HideBanner();
            else ads.ShowBanner();
        }
    }
}
