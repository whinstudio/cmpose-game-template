namespace Kturker.UnityBase.Core.Presentation
{
    // The menu's currency counter at the top bar's left (the economy writes it)
    public interface IMenuCoins
    {
        // null count hides the counter (no economy); mark: the currency's glyph or fallback disc in its own colour with a
        // darker rim (FallbackIcon.ShowCurrency; null: the plain disc)
        void SetCoins(int? count, CurrencyMark mark);
    }
}
