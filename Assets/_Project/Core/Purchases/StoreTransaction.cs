using System;

namespace Kturker.UnityBase.Core.Purchases
{
    // One store purchase as RevenueCat reports it: its transaction id (the same on every refresh, so grants can be
    // counted once), the product bought and when, in UTC
    public sealed class StoreTransaction
    {
        public StoreTransaction(string id, string productId, DateTime purchasedUtc)
        {
            Id = id;
            ProductId = productId;
            PurchasedUtc = purchasedUtc;
        }

        public string Id { get; }
        public string ProductId { get; }
        public DateTime PurchasedUtc { get; }
    }
}
