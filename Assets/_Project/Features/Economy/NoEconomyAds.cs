using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Features.Economy
{
    // Without the ads flag no window offers an ad
    public sealed class NoEconomyAds : IEconomyAds
    {
        public bool RewardedAvailable => false;

        public UniTask<bool> ShowRewardedAsync(CancellationToken ct) => UniTask.FromResult(false);

        public void HoldBanner(bool held)
        {
        }
    }
}
