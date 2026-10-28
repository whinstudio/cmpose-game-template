namespace Kturker.UnityBase.Core.Purchases
{
    // Keys of Resources/Strings/<language>/purchases.json
    public static class PurchasesKeys
    {
        public const string SettingsTitle = "settings.premium";
        public const string StateGet = "purchases.state.get";
        public const string StateActive = "purchases.state.active";
        public const string StateUnavailable = "purchases.state.unavailable";
        public const string Title = "purchases.shop.title";
        public const string Restore = "purchases.restore";
        public const string Restored = "purchases.restored";
        public const string Retry = "purchases.retry";
        public const string Unavailable = "purchases.unavailable";
        // The line of a row that turns the ads off (remove_ads, no_ads_bundle) and a row's discount label ("-40%")
        public const string NoAds = "purchases.shop.noAds";
        public const string Tag = "purchases.shop.tag";

        // A shop row's title (ShopProducts ids)
        public static string RowTitle(string id) => $"purchases.shop.{id}.title";
        public const string ErrorUnavailable = "purchases.error.unavailable";
        public const string ErrorPackageUnavailable = "purchases.error.packageUnavailable";
        public const string ErrorNothingToRestore = "purchases.error.nothingToRestore";

        public static string Error(StoreErrorKind kind)
        {
            switch (kind)
            {
                case StoreErrorKind.Network: return "purchases.error.network";
                case StoreErrorKind.Pending: return "purchases.error.pending";
                case StoreErrorKind.Store: return "purchases.error.store";
                case StoreErrorKind.NotAllowed: return "purchases.error.notAllowed";
                case StoreErrorKind.ProductUnavailable: return ErrorPackageUnavailable;
                default: return "purchases.error.failed";
            }
        }
    }
}
