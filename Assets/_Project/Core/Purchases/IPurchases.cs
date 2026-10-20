using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Purchases
{
    // What the game uses: is premium on, and the paywall's offer, purchase and restore. When IsAvailable is false
    // (release build with a Test Store key, the editor) nothing reaches RevenueCat: OfferAsync is null, the others Error.
    public interface IPurchases
    {
        bool IsAvailable { get; }
        bool IsPremium { get; }

        // After IsPremium changes
        event Action Changed;

        // After a PurchaseAsync succeeds; never for a restore or a refresh that finds premium on
        event Action PremiumPurchased;

        UniTask<PaywallOffer> OfferAsync(CancellationToken cancellation);
        UniTask<PurchaseResult> PurchaseAsync(string packageId, CancellationToken cancellation);
        UniTask<PurchaseResult> RestoreAsync(CancellationToken cancellation);
    }
}
