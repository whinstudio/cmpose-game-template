using System;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Level n" complete: stars (0-3) fill one by one, the reward counts up, Next (the big Play button), Menu
    public interface ILevelCompleteCard : IShellCard
    {
        // Each star as it lands
        event Action StarFilled;

        // settled: the stars and the reward at their end state at once (an automation run's store shots)
        void Show(int level, int stars, int reward, bool settled);

        void Celebrate();
    }
}
