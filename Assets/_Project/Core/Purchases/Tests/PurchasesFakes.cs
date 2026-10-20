using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class FakeRevenueCatGateway : IRevenueCatGateway
    {
        public static readonly IReadOnlyCollection<string> Premium = new[] { PurchasesConfig.PremiumEntitlement };
        public static readonly IReadOnlyCollection<string> Free = new string[0];

        public readonly List<string> ConfiguredKeys = new List<string>();
        public readonly List<string> Purchased = new List<string>();
        public int OfferingCalls;
        public int Restores;
        public int CustomerCalls;
        public StoreReply<IReadOnlyList<StorePackage>> Offerings = StoreReply<IReadOnlyList<StorePackage>>.Ok(new StorePackage[0]);
        public StoreReply<IReadOnlyCollection<string>> PurchaseReply = StoreReply<IReadOnlyCollection<string>>.Ok(Premium);
        public StoreReply<IReadOnlyCollection<string>> RestoreReply = StoreReply<IReadOnlyCollection<string>>.Ok(Free);
        public StoreReply<IReadOnlyCollection<string>> CustomerReply = StoreReply<IReadOnlyCollection<string>>.Ok(Free);
        // When set, CustomerInfoAsync waits for it (RevenueCat has not answered yet)
        public UniTaskCompletionSource<StoreReply<IReadOnlyCollection<string>>> PendingCustomer;
        private Action<IReadOnlyCollection<string>> listener;

        public void Configure(string apiKey, Action<IReadOnlyCollection<string>> customerInfoUpdated)
        {
            ConfiguredKeys.Add(apiKey);
            listener = customerInfoUpdated;
        }

        public void Push(IReadOnlyCollection<string> entitlements) => listener(entitlements);

        public UniTask<StoreReply<IReadOnlyList<StorePackage>>> OfferingsAsync()
        {
            OfferingCalls++;
            return UniTask.FromResult(Offerings);
        }

        public UniTask<StoreReply<IReadOnlyCollection<string>>> PurchaseAsync(string packageId)
        {
            Purchased.Add(packageId);
            return UniTask.FromResult(PurchaseReply);
        }

        public UniTask<StoreReply<IReadOnlyCollection<string>>> RestoreAsync()
        {
            Restores++;
            return UniTask.FromResult(RestoreReply);
        }

        public UniTask<StoreReply<IReadOnlyCollection<string>>> CustomerInfoAsync()
        {
            CustomerCalls++;
            return PendingCustomer != null ? PendingCustomer.Task : UniTask.FromResult(CustomerReply);
        }
    }

    public sealed class FakePurchases : IPurchases
    {
        public bool IsAvailable { get; set; } = true;
        public bool IsPremium { get; set; }
        public event Action Changed;
        public PaywallOffer Offer;
        // When set, the call waits for it
        public UniTaskCompletionSource<PaywallOffer> PendingOffer;
        public UniTaskCompletionSource<PurchaseResult> PendingPurchase;
        public PurchaseResult PurchaseResult = PurchaseResult.Success;
        public PurchaseResult RestoreResult = PurchaseResult.Success;
        public readonly List<string> Purchased = new List<string>();
        public int OfferCalls;
        public int Restores;
        public CancellationToken LastOfferToken;

        public event Action PremiumPurchased;

        public void RaiseChanged() => Changed?.Invoke();
        public void RaisePremiumPurchased() => PremiumPurchased?.Invoke();

        public UniTask<PaywallOffer> OfferAsync(CancellationToken cancellation)
        {
            OfferCalls++;
            LastOfferToken = cancellation;
            return PendingOffer != null ? PendingOffer.Task : UniTask.FromResult(Offer);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string packageId, CancellationToken cancellation)
        {
            Purchased.Add(packageId);
            return PendingPurchase != null ? PendingPurchase.Task : UniTask.FromResult(PurchaseResult);
        }

        public UniTask<PurchaseResult> RestoreAsync(CancellationToken cancellation)
        {
            Restores++;
            return PendingPurchase != null ? PendingPurchase.Task : UniTask.FromResult(RestoreResult);
        }
    }

    public sealed class FakePaywall : IPaywall
    {
        public readonly List<PaywallSource> Shown = new List<PaywallSource>();
        public void Show(PaywallSource source) => Shown.Add(source);
    }
}
