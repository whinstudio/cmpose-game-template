using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sdk = global::Purchases;

namespace Kturker.UnityBase.Core.Purchases
{
    // The only class that sees RevenueCat's Unity SDK. Purchases makes its native wrapper in Start, so Configure
    // waits one frame after adding it; every call waits for Configure. The SDK keeps one callback per API, so
    // overlapping offering and customer-info calls share one SDK call. Sdk is RevenueCat's global Purchases class,
    // whose name this namespace hides.
    public sealed class RevenueCatGateway : IRevenueCatGateway
    {
        public const string ObjectName = "CmposePurchases";

        private readonly UniTaskCompletionSource ready = new UniTaskCompletionSource();
        // The packages of the last offering by their identifier (the product id)
        private readonly Dictionary<string, Sdk.Package> packages = new Dictionary<string, Sdk.Package>();
        private Sdk purchases;
        private UniTask<StoreReply<IReadOnlyList<ShopProduct>>> offerings;
        private bool offeringsRunning;
        private UniTask<StoreReply<CustomerSnapshot>> customer;
        private bool customerRunning;

        public void Configure(string apiKey, Action<CustomerSnapshot> updated)
        {
            if (purchases != null) return;
            var go = new GameObject(ObjectName);
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);
            purchases = go.AddComponent<Sdk>();
            purchases.useRuntimeSetup = true;
            var listener = go.AddComponent<CustomerInfoListener>();
            listener.Received = info => updated(Snapshot(info));
            purchases.listener = listener;
            go.SetActive(true);
            ConfigureNextFrameAsync(apiKey).Forget();
        }

        public UniTask<StoreReply<IReadOnlyList<ShopProduct>>> OfferingsAsync()
        {
            if (!offeringsRunning)
            {
                offeringsRunning = true;
                offerings = FetchOfferingsAsync().Preserve();
            }
            return offerings;
        }

        public async UniTask<StoreReply<PurchaseReply>> PurchaseAsync(string productId)
        {
            await ready.Task;
            if (!packages.TryGetValue(productId, out var package))
                return StoreReply<PurchaseReply>.Failed(StoreErrorKind.ProductUnavailable);
            var source = new UniTaskCompletionSource<StoreReply<PurchaseReply>>();
            purchases.PurchasePackage(package, result =>
            {
                if (result.UserCancelled) source.TrySetResult(StoreReply<PurchaseReply>.Failed(StoreErrorKind.Cancelled));
                else if (result.Error != null) source.TrySetResult(StoreReply<PurchaseReply>.Failed(StoreErrors.Kind(result.Error.Code)));
                else source.TrySetResult(StoreReply<PurchaseReply>.Ok(new PurchaseReply(Snapshot(result.CustomerInfo), Transaction(result.StoreTransaction))));
            });
            return await source.Task;
        }

        public async UniTask<StoreReply<CustomerSnapshot>> RestoreAsync()
        {
            await ready.Task;
            var source = new UniTaskCompletionSource<StoreReply<CustomerSnapshot>>();
            purchases.RestorePurchases((info, error) => source.TrySetResult(CustomerReply(info, error)));
            return await source.Task;
        }

        public UniTask<StoreReply<CustomerSnapshot>> CustomerInfoAsync()
        {
            if (!customerRunning)
            {
                customerRunning = true;
                customer = FetchCustomerAsync().Preserve();
            }
            return customer;
        }

        private async UniTaskVoid ConfigureNextFrameAsync(string apiKey)
        {
            await UniTask.NextFrame();
            purchases.Configure(Sdk.PurchasesConfiguration.Builder.Init(apiKey).Build());
            ready.TrySetResult();
        }

        private async UniTask<StoreReply<IReadOnlyList<ShopProduct>>> FetchOfferingsAsync()
        {
            try
            {
                await ready.Task;
                var source = new UniTaskCompletionSource<StoreReply<IReadOnlyList<ShopProduct>>>();
                purchases.GetOfferings((all, error) =>
                {
                    if (error != null)
                    {
                        source.TrySetResult(StoreReply<IReadOnlyList<ShopProduct>>.Failed(StoreErrors.Kind(error.Code)));
                        return;
                    }
                    packages.Clear();
                    var current = all?.Current;
                    if (current == null)
                    {
                        source.TrySetResult(StoreReply<IReadOnlyList<ShopProduct>>.Ok(null));
                        return;
                    }
                    foreach (var package in current.AvailablePackages) packages[package.Identifier] = package;
                    source.TrySetResult(StoreReply<IReadOnlyList<ShopProduct>>.Ok(current.AvailablePackages
                        .Select(p => new ShopProduct(p.Identifier, p.StoreProduct.PriceString)).ToList()));
                });
                return await source.Task;
            }
            finally
            {
                offeringsRunning = false;
            }
        }

        private async UniTask<StoreReply<CustomerSnapshot>> FetchCustomerAsync()
        {
            try
            {
                await ready.Task;
                var source = new UniTaskCompletionSource<StoreReply<CustomerSnapshot>>();
                purchases.GetCustomerInfo((info, error) => source.TrySetResult(CustomerReply(info, error)));
                return await source.Task;
            }
            finally
            {
                customerRunning = false;
            }
        }

        private static StoreReply<CustomerSnapshot> CustomerReply(Sdk.CustomerInfo info, Sdk.Error error) =>
            error != null
                ? StoreReply<CustomerSnapshot>.Failed(StoreErrors.Kind(error.Code))
                : StoreReply<CustomerSnapshot>.Ok(Snapshot(info));

        // RevenueCat's model classes are plain C# objects, so ?. is safe here
        private static CustomerSnapshot Snapshot(Sdk.CustomerInfo info)
        {
            if (info == null) return CustomerSnapshot.Empty;
            var active = info.Entitlements?.Active?.Keys.ToList() ?? new List<string>();
            var transactions = info.NonSubscriptionTransactions?.Select(Transaction).Where(t => t != null).ToList()
                ?? new List<StoreTransaction>();
            return new CustomerSnapshot(active, transactions);
        }

        private static StoreTransaction Transaction(Sdk.StoreTransaction transaction) =>
            transaction == null || string.IsNullOrEmpty(transaction.TransactionIdentifier)
                ? null
                : new StoreTransaction(transaction.TransactionIdentifier, transaction.ProductIdentifier,
                    Utc(transaction.PurchaseDate));

        // RevenueCat reads the date from epoch milliseconds; keep the instant whatever Kind the SDK gives it
        private static DateTime Utc(DateTime date) =>
            date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }
}
