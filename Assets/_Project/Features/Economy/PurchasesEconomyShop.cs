using Kturker.UnityBase.Core.Purchases;

namespace Kturker.UnityBase.Features.Economy
{
    // purchases flag: the economy's shop buttons open IShop
    public sealed class PurchasesEconomyShop : IEconomyShop
    {
        private readonly IShop shop;

        public PurchasesEconomyShop(IShop shop) => this.shop = shop;

        public bool CanShow => shop.CanShow;

        public void Show(bool notEnough) => shop.Show(notEnough ? ShopSource.NotEnough : ShopSource.Currency);
    }
}
