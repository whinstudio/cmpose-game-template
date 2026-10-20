using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The game scene's standard parts behind one interface: the HUD (score, best, an optional counter, short toasts),
    // the start hint and the end cards. Game code calls it and never draws these itself. Call Begin before the rest;
    // every card plays at timeScale 0 too. The game acts on the returned choice (Retry: a new run, Menu: load the
    // menu, Continue: revive, Next: Begin the next level).
    public interface IGameShell
    {
        ShellMode Mode { get; }

        // Shows the HUD for a new run or level. Levels without a save store (saveSystem off) is a mistake: an
        // InvalidOperationException in the editor, a logged error and the endless HUD on a device.
        void Begin(ShellMode mode);

        // Counts the HUD's score up (or down) to score
        void SetScore(int score);

        // The HUD's "Best: n" line (endless)
        void SetBest(int best);

        // An optional second counter (coins, lives); hidden until the first call
        void SetCounter(int value);

        // The counter's icon: a currency's glyph or fallback disc in its own colour (the economy sets it); null: the
        // plain Accent dot
        void SetCounterIcon(CurrencyMark mark);

        // A short localized message that pops and fades, e.g. "shell.perfect"
        void Toast(string key);

        void ShowStartHint();
        void HideStartHint();

        // Retry | Menu | Continue (Continue only while a rewarded ad is on offer, once per run)
        UniTask<EndChoice> ShowGameOver(int score, int best, bool newBest);

        // Next | Menu; stars 0-3 fill one by one, the reward counts up
        UniTask<EndChoice> ShowLevelComplete(int level, int stars, int reward);

        // Retry | Continue | Menu
        UniTask<EndChoice> ShowLevelFailed(int level);

        // The level to play (1 first); 1 without a save store
        int CurrentLevel { get; }

        // Keeps the level's best stars (0-3) and moves CurrentLevel past it
        void CompleteLevel(int level, int stars);
    }
}
