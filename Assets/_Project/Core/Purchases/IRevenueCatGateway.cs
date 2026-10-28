using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Purchases
{
    // RevenueCat's SDK behind plain types (RevenueCatGateway is the only class that sees the SDK). Customer info is a
    // CustomerSnapshot. Calls made before Configure has finished wait for it.
    public interface IRevenueCatGateway
    {
        void Configure(string apiKey, Action<CustomerSnapshot> updated);

        // Ok(null): RevenueCat has no current offering
        UniTask<StoreReply<IReadOnlyList<ShopProduct>>> OfferingsAsync();

        // productId must come from the last OfferingsAsync
        UniTask<StoreReply<PurchaseReply>> PurchaseAsync(string productId);

        UniTask<StoreReply<CustomerSnapshot>> RestoreAsync();

        UniTask<StoreReply<CustomerSnapshot>> CustomerInfoAsync();
    }
}
