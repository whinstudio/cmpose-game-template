namespace Kturker.UnityBase.Core.Purchases
{
    // Opens the shop above every scene; Features/Shell draws it. Showing an open shop does nothing, nor does showing one
    // that cannot show (CanShow false) or during an AutoPlay run.
    public interface IShop
    {
        // Purchases are available and the shop has a row to sell (one the player does not own yet); the entries hide
        // while it is false
        bool CanShow { get; }

        void Show(ShopSource source);
    }
}
