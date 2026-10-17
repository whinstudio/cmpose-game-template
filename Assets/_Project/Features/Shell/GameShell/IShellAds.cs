using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The shell's ad hooks: Continue on the end cards (a rewarded ad) and a natural break after a run or level ends
    // (an interstitial; the pacer may skip it). NoShellAds without the ads flag; the ads flag registers RewardedShellAds.
    public interface IShellAds
    {
        // A rewarded ad (or premium's free continue) is on offer
        bool RewardedAvailable { get; }

        // Premium continues without an ad: the label drops "(ad)"
        bool ContinueIsFree { get; }

        // True when the reward is earned
        UniTask<bool> ShowRewardedAsync(CancellationToken cancellation);

        UniTask ShowBreakAsync(CancellationToken cancellation);
    }
}
