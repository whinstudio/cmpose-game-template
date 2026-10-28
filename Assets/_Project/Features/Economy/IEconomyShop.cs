namespace Kturker.UnityBase.Features.Economy
{
    // The shop as the economy's screens open it: the menu's currency counter and the booster offer's "not enough"
    // moment. NoEconomyShop without the purchases flag; PurchasesEconomyShop over IShop with it.
    public interface IEconomyShop
    {
        // The shop has something to sell now; its buttons hide while false
        bool CanShow { get; }

        // notEnough: from a booster offer the coins do not reach; else from the currency counter
        void Show(bool notEnough);
    }

    public sealed class NoEconomyShop : IEconomyShop
    {
        public bool CanShow => false;

        public void Show(bool notEnough)
        {
        }
    }
}
