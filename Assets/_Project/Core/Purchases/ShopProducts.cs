namespace Kturker.UnityBase.Core.Purchases
{
    // The product ids every game may sell (the RevenueCat package ids of the current offering and the store product
    // ids). One-time products are non-consumable: the store sells them once. bundle_1..3 and coins_1..6 are
    // consumable and may be bought again and again. remove_ads and no_ads_bundle unlock the premium entitlement.
    public static class ShopProducts
    {
        public const string RemoveAds = "remove_ads";
        public const string StarterPack = "starter_pack";
        public const string NoAdsBundle = "no_ads_bundle";

        public static bool IsOneTime(string id) => id == RemoveAds || id == StarterPack || id == NoAdsBundle;

        // remove_ads and no_ads_bundle: the rows a game without ads, or a player with premium, never sees
        public static bool GrantsPremium(string id) => id == RemoveAds || id == NoAdsBundle;

        // coins_1..6: the shop's grid of coin packs
        public static bool IsCoinPack(string id) => id != null && id.StartsWith("coins_", System.StringComparison.Ordinal);
    }
}
