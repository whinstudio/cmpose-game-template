using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // Without the ads flag: no Continue, no breaks
    public sealed class NoShellAds : IShellAds
    {
        public bool RewardedAvailable => false;
        public bool ContinueIsFree => false;

        public UniTask<bool> ShowRewardedAsync(CancellationToken cancellation) => UniTask.FromResult(false);

        public UniTask ShowBreakAsync(CancellationToken cancellation) => UniTask.CompletedTask;
    }
}
