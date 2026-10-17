using Kturker.UnityBase.Core.Localization;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Level n" failed: Continue, the breathing Retry and Menu
    public sealed class LevelFailedCard : ShellCardView, ILevelFailedCard
    {
        [SerializeField] private LocalizedText title;

        protected override EndChoice Primary => EndChoice.Retry;

        public void Show(int level)
        {
            title.SetKey(ShellKeys.Level, level);
            Open();
        }
    }
}
