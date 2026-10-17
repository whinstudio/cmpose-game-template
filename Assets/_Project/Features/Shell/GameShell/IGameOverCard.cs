namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // Game over: the score counting up, "Best: n", Retry (the big Play button), Menu, Continue
    public interface IGameOverCard : IShellCard
    {
        void Show(int score, int best);

        // "New best!" pops in with confetti
        void CelebrateNewBest();
    }
}
