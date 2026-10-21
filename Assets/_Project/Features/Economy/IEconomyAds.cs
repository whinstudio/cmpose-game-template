using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy's rewarded ads: NoEconomyAds without the ads flag, RewardedEconomyAds with it
    public interface IEconomyAds
    {
        // An ad can be offered (never during an AutoPlay run)
        bool RewardedAvailable { get; }

        // True only when an ad was shown and its reward earned: not ready, closed early or failed is false
        UniTask<bool> ShowRewardedAsync(CancellationToken ct);

        // While held (a menu window is open) the menu's banner hides and its room stays; released, it shows again
        void HoldBanner(bool held);
    }
}
