namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Level n" failed: Retry (the big Play button), Continue, Menu
    public interface ILevelFailedCard : IShellCard
    {
        void Show(int level);
    }
}
