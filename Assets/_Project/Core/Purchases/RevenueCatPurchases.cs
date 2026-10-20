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
    // last known premium state is kept in the preferences, so a returning premium user is premium offline too.
    public sealed class RevenueCatPurchases : IPurchases, IPremiumStatus, IStartable
    {
        public const string PremiumKey = "purchases.premium";

        private readonly IRevenueCatGateway gateway;
        private readonly IPreferences preferences;
        private readonly ILog log;
        private readonly string apiKey;
        // The packages of the last offer: the SDK can buy only those
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

        public event Action Changed;
        public event Action PremiumPurchased;

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

        public async UniTask<PaywallOffer> OfferAsync(CancellationToken cancellation)
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
                log.Warning("No current offering: mark one current in the RevenueCat dashboard");
                return null;
            }
            offeredIds = new HashSet<string>(reply.Value.Select(p => p.Id));
            return PaywallMath.BuildOffer(reply.Value);
        }

        public async UniTask<PurchaseResult> PurchaseAsync(string packageId, CancellationToken cancellation)
        {
            if (!IsAvailable) return PurchaseResult.Error(PurchasesKeys.ErrorUnavailable);
            // The paywall may have been opened before RevenueCat answered, or the offering changed: ask once more
            if (!offeredIds.Contains(packageId)) await OfferAsync(cancellation);
            if (!offeredIds.Contains(packageId)) return PurchaseResult.Error(PurchasesKeys.ErrorPackageUnavailable);
            var reply = await gateway.PurchaseAsync(packageId).AttachExternalCancellation(cancellation);
            if (!reply.IsOk) return Failed("Purchase", reply.Error);
            Update(reply.Value);
            PremiumPurchased?.Invoke();
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

        private void Update(IReadOnlyCollection<string> activeEntitlements)
        {
            var premium = activeEntitlements != null && activeEntitlements.Contains(PurchasesConfig.PremiumEntitlement);
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
