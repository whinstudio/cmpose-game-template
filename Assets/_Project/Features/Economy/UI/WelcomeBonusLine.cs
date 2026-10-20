using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Features.Economy
{
    // E7 on the paywall: the welcome bundle a premium purchase pays, "+600 acorns, +3 Bomb" (the currency first, by its
    // name, then the boosters in the config's order). Nothing once it was given, with the economy off or with an empty
    // bundle. The currency's part is keyed economy.currency.name, or the file's name itself while the game's tables do
    // not name it (the paywall writes an unknown key as it is).
    public sealed class WelcomeBonusLine : IPaywallBonus
    {
        private readonly Economy economy;
        private readonly ILocalizer localizer;

        public WelcomeBonusLine(Economy economy, ILocalizer localizer)
        {
            this.economy = economy;
            this.localizer = localizer;
        }

        public string LineKey => Args.Count > 0 ? EconomyKeys.WelcomeLine : null;

        public IReadOnlyDictionary<string, string> Args
        {
            get
            {
                var parts = new Dictionary<string, string>();
                if (!economy.Enabled || economy.Wallet.State.WelcomeGiven) return parts;
                var welcome = economy.Config.Welcome;
                if (welcome.Coins > 0) parts[CurrencyKey()] = Number(welcome.Coins);
                foreach (var booster in economy.Config.Boosters)
                {
                    if (welcome.Boosters.TryGetValue(booster.Id, out var count) && count > 0) parts[booster.NameKey] = Number(count);
                }
                return parts;
            }
        }

        private string CurrencyKey()
        {
            var named = localizer?.Get(EconomyKeys.CurrencyName);
            return string.IsNullOrWhiteSpace(named) || named == EconomyKeys.CurrencyName ? economy.Config.Currency.Name : EconomyKeys.CurrencyName;
        }

        private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
