using System;

namespace Kturker.UnityBase.Core.Purchases
{
    // RevenueCat keys and the entitlement that means premium. The generator writes these lines (like ApiConfig). The
    // template ships RevenueCat's Test Store key on both platforms: replace it with your app's public keys before you
    // ship, because RevenueCat closes a release build that uses a Test Store key (CanConfigure keeps it off instead).
    // On iOS RevenueCat judges the Xcode configuration, not Unity's: build a Development player with Xcode's Debug
    // configuration (Run), never Release or Archive, while the key is a Test Store key.
    public static class PurchasesConfig
    {
        public const string ApiKeyApple = "test_qmHxQuucnBoXDlJVLUGsCxcGXrw";
        public const string ApiKeyGoogle = "test_qmHxQuucnBoXDlJVLUGsCxcGXrw";
        public const string PremiumEntitlement = "premium";
        // The shop sells the packages of this offering; without it, those of the current offering. A RevenueCat project
        // shared with apps keeps their paywall in the current offering and the games' products here.
        public const string ShopOffering = "shop";

        public static string ApiKey(bool ios) => ios ? ApiKeyApple : ApiKeyGoogle;

        public static bool IsTestStoreKey(string apiKey) => apiKey != null && apiKey.StartsWith("test_", StringComparison.Ordinal);

        // The editor runs RevenueCat's no-op wrapper, which never answers, so it never configures either
        public static bool CanConfigure(string apiKey, bool debugBuild, bool editor) =>
            !editor && !string.IsNullOrWhiteSpace(apiKey) && (debugBuild || !IsTestStoreKey(apiKey));
    }
}
