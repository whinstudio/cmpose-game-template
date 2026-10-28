using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class FakeRevenueCatGateway : IRevenueCatGateway
    {
        public static readonly CustomerSnapshot Premium = Customer(new[] { PurchasesConfig.PremiumEntitlement });
        public static readonly CustomerSnapshot Free = Customer(new string[0]);

        public readonly List<string> ConfiguredKeys = new List<string>();
        public readonly List<string> Purchased = new List<string>();
        public int OfferingCalls;
        public int Restores;
        public int CustomerCalls;
        public StoreReply<IReadOnlyList<ShopProduct>> Offerings = StoreReply<IReadOnlyList<ShopProduct>>.Ok(new ShopProduct[0]);
        public StoreReply<PurchaseReply> PurchaseReply = StoreReply<PurchaseReply>.Ok(new PurchaseReply(Premium, null));
        public StoreReply<CustomerSnapshot> RestoreReply = StoreReply<CustomerSnapshot>.Ok(Free);
        public StoreReply<CustomerSnapshot> CustomerReply = StoreReply<CustomerSnapshot>.Ok(Free);
        // When set, CustomerInfoAsync waits for it (RevenueCat has not answered yet)
        public UniTaskCompletionSource<StoreReply<CustomerSnapshot>> PendingCustomer;
        private Action<CustomerSnapshot> listener;

        public static CustomerSnapshot Customer(IReadOnlyCollection<string> entitlements, params StoreTransaction[] transactions) =>
            new CustomerSnapshot(entitlements, transactions);

        public static StoreTransaction Transaction(string id, string productId) =>
            new StoreTransaction(id, productId, new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc));

        public static StoreReply<IReadOnlyList<ShopProduct>> Offering(params string[] productIds) =>
            StoreReply<IReadOnlyList<ShopProduct>>.Ok(productIds.Select(id => new ShopProduct(id, "$0.99")).ToList());

        // A successful purchase whose customer info and transaction are these
        public static StoreReply<PurchaseReply> Bought(CustomerSnapshot customer, StoreTransaction transaction) =>
            StoreReply<PurchaseReply>.Ok(new PurchaseReply(customer, transaction));

        public void Configure(string apiKey, Action<CustomerSnapshot> updated)
        {
            ConfiguredKeys.Add(apiKey);
            listener = updated;
        }

        public void Push(CustomerSnapshot customer) => listener(customer);

        public UniTask<StoreReply<IReadOnlyList<ShopProduct>>> OfferingsAsync()
        {
            OfferingCalls++;
            return UniTask.FromResult(Offerings);
        }

        public UniTask<StoreReply<PurchaseReply>> PurchaseAsync(string productId)
        {
            Purchased.Add(productId);
            return UniTask.FromResult(PurchaseReply);
        }

        public UniTask<StoreReply<CustomerSnapshot>> RestoreAsync()
        {
            Restores++;
            return UniTask.FromResult(RestoreReply);
        }

        public UniTask<StoreReply<CustomerSnapshot>> CustomerInfoAsync()
        {
            CustomerCalls++;
            return PendingCustomer != null ? PendingCustomer.Task : UniTask.FromResult(CustomerReply);
        }
    }

    public sealed class FakePurchases : IPurchases
    {
        public bool IsAvailable { get; set; } = true;
        public bool IsPremium { get; set; }
        public IReadOnlyList<StoreTransaction> Transactions { get; set; } = new StoreTransaction[0];
        public event Action Changed;
        public event Action<StoreTransaction> ProductPurchased;
        public event Action<IReadOnlyList<StoreTransaction>> TransactionsUpdated;
        public IReadOnlyList<ShopProduct> Products;
        // When set, the call waits for it
        public UniTaskCompletionSource<IReadOnlyList<ShopProduct>> PendingProducts;
        public UniTaskCompletionSource<PurchaseResult> PendingPurchase;
        public PurchaseResult PurchaseResult = PurchaseResult.Success;
        public PurchaseResult RestoreResult = PurchaseResult.Success;
        public readonly List<string> Purchased = new List<string>();
        public int ProductCalls;
        public int Restores;
        public CancellationToken LastProductsToken;

        public void RaiseChanged() => Changed?.Invoke();
        public void RaiseProductPurchased(StoreTransaction transaction) => ProductPurchased?.Invoke(transaction);

        public void RaiseTransactionsUpdated(IReadOnlyList<StoreTransaction> transactions)
        {
            Transactions = transactions;
            TransactionsUpdated?.Invoke(transactions);
        }

        public bool Owns(string productId) => Transactions.Any(t => t.ProductId == productId);

        public UniTask<IReadOnlyList<ShopProduct>> ProductsAsync(CancellationToken cancellation)
        {
            ProductCalls++;
            LastProductsToken = cancellation;
            return PendingProducts != null ? PendingProducts.Task : UniTask.FromResult(Products);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellation)
        {
            Purchased.Add(productId);
            return PendingPurchase != null ? PendingPurchase.Task : UniTask.FromResult(PurchaseResult);
        }

        public UniTask<PurchaseResult> RestoreAsync(CancellationToken cancellation)
        {
            Restores++;
            return PendingPurchase != null ? PendingPurchase.Task : UniTask.FromResult(RestoreResult);
        }
    }

    public sealed class FakeShop : IShop
    {
        public readonly List<ShopSource> Shown = new List<ShopSource>();
        public bool CanShow { get; set; } = true;
        public void Show(ShopSource source) => Shown.Add(source);
    }
}
