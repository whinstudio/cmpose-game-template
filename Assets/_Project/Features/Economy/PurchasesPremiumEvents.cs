using System;
using Kturker.UnityBase.Core.Purchases;

namespace Kturker.UnityBase.Features.Economy
{
    // purchases flag: IPurchases.PremiumPurchased (raised by a successful PurchaseAsync only)
    public sealed class PurchasesPremiumEvents : IPremiumPurchaseEvents
    {
        private readonly IPurchases purchases;

        public PurchasesPremiumEvents(IPurchases purchases) => this.purchases = purchases;

        public event Action PremiumPurchased
        {
            add => purchases.PremiumPurchased += value;
            remove => purchases.PremiumPurchased -= value;
        }
    }
}
