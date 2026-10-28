using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Preferences;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Purchases
{
    // IPurchases over RevenueCat. Start configures the SDK (only when available) and asks for the customer info; the
    // last known premium state is kept in the preferences, so a returning premium user is premium offline too (a Test
    // Store premium from before the shop counts as Remove Ads). The last customer info's transactions are kept.
    public sealed class RevenueCatPurchases : IPurchases, IPremiumStatus, IStartable
    {
        public const string PremiumKey = "purchases.premium";

        private readonly IRevenueCatGateway gateway;
        private readonly IPreferences preferences;
        private readonly ILog log;
        private readonly string apiKey;
        // The products of the last offering: the SDK can buy only those
        private HashSet<string> offeredIds = new HashSet<string>();

        public RevenueCatPurchases(IRevenueCatGateway gateway, IPreferences preferences, ILog log, string apiKey, bool available)
        {
            this.gateway = gateway;
            this.preferences = preferences;
            this.log = log;
            this.apiKey = apiKey;
            IsAvailable = available;
            IsPremium = preferences.GetBool(PremiumKey, false);
        }

        public bool IsAvailable { get; }
        public bool IsPremium { get; private set; }
        public IReadOnlyList<StoreTransaction> Transactions { get; private set; } = new StoreTransaction[0];

        public event Action Changed;
        public event Action<StoreTransaction> ProductPurchased;
        public event Action<IReadOnlyList<StoreTransaction>> TransactionsUpdated;

        public void Start()
        {
            Mark();
            if (!IsAvailable)
            {
                log.Warning("Purchases are off (a release build with a Test Store key, or the editor): add your keys to PurchasesConfig");
                return;
            }
            if (PurchasesConfig.IsTestStoreKey(apiKey)) log.Warning("TEST KEYS - replace the keys in PurchasesConfig before you ship");
            gateway.Configure(apiKey, Update);
            RefreshAsync().Forget();
        }

        public bool Owns(string productId) => Transactions.Any(t => t.ProductId == productId);

        public async UniTask<IReadOnlyList<ShopProduct>> ProductsAsync(CancellationToken cancellation)
        {
            if (!IsAvailable) return null;
            var reply = await gateway.OfferingsAsync().AttachExternalCancellation(cancellation);
            if (!reply.IsOk)
            {
                log.Warning($"Could not load the offerings: {reply.Error}");
                return null;
            }
            if (reply.Value == null)
            {
                offeredIds = new HashSet<string>();
                log.Warning("No shop offering: add a 'shop' offering (or mark one current) in the RevenueCat dashboard");
                return null;
            }
            offeredIds = new HashSet<string>(reply.Value.Select(p => p.Id));
            return reply.Value;
        }

        public async UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellation)
        {
            if (!IsAvailable) return PurchaseResult.Error(PurchasesKeys.ErrorUnavailable);
            // The shop may have been opened before RevenueCat answered, or the offering changed: ask once more
            if (!offeredIds.Contains(productId)) await ProductsAsync(cancellation);
            if (!offeredIds.Contains(productId)) return PurchaseResult.Error(PurchasesKeys.ErrorPackageUnavailable);
            var reply = await gateway.PurchaseAsync(productId).AttachExternalCancellation(cancellation);
            if (!reply.IsOk) return Failed("Purchase", reply.Error);
            Update(reply.Value.Customer);
            // The purchase result carries the store's transaction id, the customer info RevenueCat's ("o1_…") for the same
            // purchase: report the product's latest customer info entry, so both paths name the purchase alike and it is
            // paid once (the SDK's listener may have delivered that customer info before the purchase callback)
            var bought = Transactions.Where(t => t.ProductId == productId).OrderByDescending(t => t.PurchasedUtc).FirstOrDefault()
                ?? reply.Value.Transaction;
            if (bought != null) ProductPurchased?.Invoke(bought);
            else log.Warning($"Purchase of {productId} came without a transaction: the next customer info carries it");
            return PurchaseResult.Success;
        }

        // A restore succeeds only when it brings premium back
        public async UniTask<PurchaseResult> RestoreAsync(CancellationToken cancellation)
        {
            if (!IsAvailable) return PurchaseResult.Error(PurchasesKeys.ErrorUnavailable);
            var reply = await gateway.RestoreAsync().AttachExternalCancellation(cancellation);
            if (!reply.IsOk) return Failed("Restore", reply.Error);
            Update(reply.Value);
            return IsPremium ? PurchaseResult.Success : PurchaseResult.Error(PurchasesKeys.ErrorNothingToRestore);
        }

        private async UniTaskVoid RefreshAsync()
        {
            var reply = await gateway.CustomerInfoAsync();
            if (reply.IsOk) Update(reply.Value);
            else log.Warning($"Could not refresh the customer info: {reply.Error}");
        }

        private PurchaseResult Failed(string what, StoreErrorKind error)
        {
            log.Warning($"{what} failed: {error}");
            return error == StoreErrorKind.Cancelled ? PurchaseResult.Cancelled : PurchaseResult.Error(PurchasesKeys.Error(error));
        }

        private void Update(CustomerSnapshot customer)
        {
            customer = customer ?? CustomerSnapshot.Empty;
            Transactions = customer.Transactions;
            UpdatePremium(customer.ActiveEntitlements.Contains(PurchasesConfig.PremiumEntitlement));
            TransactionsUpdated?.Invoke(Transactions);
        }

        private void UpdatePremium(bool premium)
        {
            if (premium == IsPremium) return;
            IsPremium = premium;
            preferences.SetBool(PremiumKey, premium);
            preferences.Save();
            Mark();
            Changed?.Invoke();
        }

        private void Mark() =>
            log.Marker($"CMPOSE_PURCHASES available={(IsAvailable ? "true" : "false")} premium={(IsPremium ? "true" : "false")}");
    }
}
