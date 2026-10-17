using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The endless game-over card: "Game over", the score counting up, "Best: n", the "New best!" badge with confetti,
    // Continue, the breathing Retry and Menu
    public sealed class GameOverCard : ShellCardView, IGameOverCard
    {
        [SerializeField] private CountUpText score;
        [SerializeField] private LocalizedText best;
        [SerializeField] private KitBadge newBest;
        [SerializeField] private Celebration celebration;

        protected override EndChoice Primary => EndChoice.Retry;

        public void Show(int scoreValue, int bestValue)
        {
            newBest.gameObject.SetActive(false);
            best.SetKey(ShellKeys.Best, bestValue);
            Open();
            score.SetValue(0);
            score.CountTo(scoreValue);
        }

        public void CelebrateNewBest()
        {
            newBest.gameObject.SetActive(true);
            newBest.Pop();
            celebration.Play();
        }
    }
}
