using System.Globalization;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Economy
{
    // The game's currency as the screens show it: its name in the player's language (economy.currency.name in the
    // game's tables, else the file's English name) and its mark (the file's icon, else a disc with the name's first
    // letter, in the file's colour). Texts take the name as an argument (Name, NameArg), never a suffix, so any name
    // reads in every language. Nothing (an empty name, no mark) with the economy off.
    public sealed class EconomyCurrency
    {
        private readonly EconomyConfig config;
        private readonly ILocalizer localizer;

        public EconomyCurrency(EconomyConfig config, ILocalizer localizer)
        {
            this.config = config;
            this.localizer = localizer;
            NameArg = new NameText(this, false);
            CapitalNameArg = new NameText(this, true);
        }

        public string Name
        {
            get
            {
                if (config == null) return string.Empty;
                var name = localizer?.Get(EconomyKeys.CurrencyName);
                return string.IsNullOrWhiteSpace(name) || name == EconomyKeys.CurrencyName ? config.Currency.Name : name;
            }
        }

        // A text argument that reads the name when the text is written, so a LocalizedText follows a language change
        public object NameArg { get; }

        // The same with a capital first letter (in the language's own casing), for the start of a line
        public object CapitalNameArg { get; }

        public CurrencyMark Mark => config == null
            ? null
            : new CurrencyMark(EconomyIcons.Currency(config), config.Currency.Colour, FallbackIcon.LetterOf(Name));

        // "80 acorns" in the player's language
        public string Amount(int amount) => localizer.Get(EconomyKeys.CurrencyAmount, amount, NameArg);

        private string Capital(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(localizer?.Language ?? "en");
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.InvariantCulture;
            }
            return char.ToUpper(name[0], culture) + name.Substring(1);
        }

        private sealed class NameText
        {
            private readonly EconomyCurrency currency;
            private readonly bool capital;

            public NameText(EconomyCurrency currency, bool capital)
            {
                this.currency = currency;
                this.capital = capital;
            }

            public override string ToString() => capital ? currency.Capital(currency.Name) : currency.Name;
        }
    }
}
