using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Purchases
{
    // RevenueCat's customer info as the gateway reads it: the active entitlement ids and every non-subscription
    // transaction (one-time and consumable products, oldest first as RevenueCat lists them)
    public sealed class CustomerSnapshot
    {
        public static readonly CustomerSnapshot Empty = new CustomerSnapshot(new string[0], new StoreTransaction[0]);

        public CustomerSnapshot(IReadOnlyCollection<string> activeEntitlements, IReadOnlyList<StoreTransaction> transactions)
        {
            ActiveEntitlements = activeEntitlements ?? new string[0];
            Transactions = transactions ?? new StoreTransaction[0];
        }

        public IReadOnlyCollection<string> ActiveEntitlements { get; }
        public IReadOnlyList<StoreTransaction> Transactions { get; }
    }
}
