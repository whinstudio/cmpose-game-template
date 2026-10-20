using System;

namespace Kturker.UnityBase.Features.Economy
{
    // A premium purchase that just succeeded (never a restore or a launch that finds premium already on).
    // NoPremiumPurchaseEvents without the purchases flag, PurchasesPremiumEvents with it.
    public interface IPremiumPurchaseEvents
    {
        event Action PremiumPurchased;
    }

    public sealed class NoPremiumPurchaseEvents : IPremiumPurchaseEvents
    {
        public event Action PremiumPurchased
        {
            add { }
            remove { }
        }
    }
}
