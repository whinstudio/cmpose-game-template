namespace Kturker.UnityBase.Features.Economy
{
    // The economy windows' string keys (Resources/Strings/<lang>/economy.json); the boosters' own names are the game's
    // (BoosterConfig.NameKey, DescriptionKey)
    public static class EconomyKeys
    {
        public const string Amount = "economy.amount";
        public const string AdNotReady = "economy.adNotReady";
        public const string RewardBooster = "economy.reward.booster";

        // The currency's name, the game's own in each language (the game's tables; EconomyCurrency falls back to the
        // file's name), and the texts that take it as an argument without any suffix: an amount ("80 acorns"), what the
        // player has ("You have 20 acorns") and the offer's "Acorns: not enough"
        public const string CurrencyName = "economy.currency.name";
        public const string CurrencyAmount = "economy.currency.amount";
        public const string CurrencyHave = "economy.currency.have";
        public const string CurrencyNotEnough = "economy.currency.notEnough";

        // A price next to the currency's icon
        public const string Price = "economy.price";

        // A custom extra's name (its menu caption and window title), the game's own in each language
        public static string ExtraName(string id) => $"economy.extra.{id}.name";

        // The menu's side buttons
        public const string Supply = "economy.supply";
        public const string Wheel = "economy.wheel";
        public const string Login = "economy.login";

        public const string SupplyTitle = "economy.supply.title";
        public const string SupplyFree = "economy.supply.free";
        public const string SupplyClaim = "economy.supply.claim";
        public const string SupplyReadyIn = "economy.supply.readyIn";
        public const string SupplyAdHeader = "economy.supply.adHeader";
        public const string SupplyWatch = "economy.supply.watch";
        public const string SupplyClaimed = "economy.supply.claimed";
        public const string SupplyLocked = "economy.supply.locked";

        public const string WheelTitle = "economy.wheel.title";
        public const string WheelSpin = "economy.wheel.spin";
        public const string WheelSpinAd = "economy.wheel.spinAd";
        public const string WheelProgress = "economy.wheel.progress";
        public const string WheelReady = "economy.wheel.ready";
        public const string WheelAdLeft = "economy.wheel.adLeft";
        public const string WheelOdds = "economy.wheel.odds";
        public const string WheelOddsLine = "economy.wheel.oddsLine";
        public const string WheelPercent = "economy.wheel.percent";
        public const string WheelWon = "economy.wheel.won";

        public const string LoginTitle = "economy.login.title";
        public const string LoginDay = "economy.login.day";
        public const string LoginClaim = "economy.login.claim";
        public const string LoginClaimed = "economy.login.claimed";
        public const string LoginNext = "economy.login.next";

        public const string OfferWatch = "economy.offer.watch";
        // The offer's button to the shop while the coins do not reach the price
        public const string Shop = "economy.shop";

        public const string PickerTitle = "economy.picker.title";
        public const string PickerHint = "economy.picker.hint";
        public const string PickerPlay = "economy.picker.play";
    }
}
