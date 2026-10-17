namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The HUD in the safe area: the score (counting), "Best: n" (endless) or "Level n" (levels), the optional counter
    // pill and the toast. Hidden until Show; the shell hides it under an end card.
    public interface IShellHudView
    {
        // level: the level number in Levels mode, null in Endless; the score shows 0 at once, best and counter hide
        void Show(int? level);
        // Hides or brings back the HUD shown last (no effect before Show)
        void SetVisible(bool visible);
        void CountScore(int score);
        void ShowBest(int best);
        void ShowCounter(int value);
        void Toast(string key);
    }
}
