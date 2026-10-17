using System;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Level n" complete: stars (0-3) fill one by one, the reward counts up, Next (the big Play button), Menu
    public interface ILevelCompleteCard : IShellCard
    {
        // Each star as it lands
        event Action StarFilled;

        void Show(int level, int stars, int reward);

        void Celebrate();
    }
}
