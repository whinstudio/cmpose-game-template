namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // Game over: the score counting up, "Best: n", Retry (the big Play button), Menu, Continue
    public interface IGameOverCard : IShellCard
    {
        // settled: the score at once, no count-up (an automation run's store shots)
        void Show(int score, int best, bool settled);

        // "New best!" pops in with confetti
        void CelebrateNewBest();
    }
}
