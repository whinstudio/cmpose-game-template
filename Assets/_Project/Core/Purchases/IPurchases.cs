using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Purchases
{
    // What the game uses: is premium (Remove Ads) on, which products were bought, and the shop's products, purchase and
    // restore. When IsAvailable is false (release build with a Test Store key, the editor) nothing reaches RevenueCat:
    // ProductsAsync is null, the others Error.
    public interface IPurchases
    {
        bool IsAvailable { get; }
        bool IsPremium { get; }

        // After IsPremium changes
        event Action Changed;

        // The non-subscription transactions of the last customer info (empty until RevenueCat answers)
        IReadOnlyList<StoreTransaction> Transactions { get; }

        // A transaction of productId is known (one-time products: already bought)
        bool Owns(string productId);

        // After a PurchaseAsync succeeds, with its transaction; never for a restore or a refresh
        event Action<StoreTransaction> ProductPurchased;

        // After every customer info (launch, refresh, purchase, restore): a purchase the app missed (closed while it
        // ran) is in the list. The same transaction comes again and again: count it once by its Id.
        event Action<IReadOnlyList<StoreTransaction>> TransactionsUpdated;

        // The current offering's products in the order of the RevenueCat dashboard; null when it could not be loaded
        UniTask<IReadOnlyList<ShopProduct>> ProductsAsync(CancellationToken cancellation);
        UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellation);
        UniTask<PurchaseResult> RestoreAsync(CancellationToken cancellation);
    }
}
