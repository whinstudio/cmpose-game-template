using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Localization;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    // Every game's EditMode gate: the code step names each booster of GameEconomy.json in every language
    public sealed class EconomyStringsTests
    {
        private static readonly string[] Languages = { "en", "tr", "fr", "es", "it" };

        [Test]
        public void Every_booster_has_strings_in_every_language()
        {
            EconomyConfig.ForTests(null);
            var config = EconomyConfig.Current;
            if (config == null) Assert.Pass("no GameEconomy.json: the economy is off");

            var source = new ResourcesStringSource();
            var missing = new List<string>();
            foreach (var language in Languages)
            {
                var keys = Keys(source.Load(language));
                foreach (var booster in config.Boosters)
                {
                    foreach (var key in new[] { booster.NameKey, booster.DescriptionKey })
                    {
                        if (!keys.Contains(key)) missing.Add($"{language}: {key}");
                    }
                }
            }
            Assert.That(missing, Is.Empty, "write these keys into Resources/Strings/<language>/ tables");
        }

        // The code step names the game's currency and each of its custom extras in every language
        [Test]
        public void Currency_and_custom_extras_have_strings_in_every_language()
        {
            EconomyConfig.ForTests(null);
            var config = EconomyConfig.Current;
            if (config == null) Assert.Pass("no GameEconomy.json: the economy is off");

            var source = new ResourcesStringSource();
            var missing = new List<string>();
            foreach (var language in Languages)
            {
                var keys = Keys(source.Load(language));
                var wanted = new List<string> { EconomyKeys.CurrencyName };
                foreach (var extra in config.Extras)
                {
                    if (extra.Kind == ExtraKind.Custom) wanted.Add(extra.NameKey);
                }
                foreach (var key in wanted)
                {
                    if (!keys.Contains(key)) missing.Add($"{language}: {key}");
                }
            }
            Assert.That(missing, Is.Empty, "write these keys into Resources/Strings/<language>/ tables");
        }

        // The currency's texts take its name as an argument and never add a suffix to it, so a name made up for the game
        // ("zorbs") reads in every language; the windows' texts use the game's name
        [Test]
        public void Currency_texts_take_any_name_in_every_language()
        {
            const string name = "zorbs";
            var localizer = new TableLocalizer(new ResourcesStringSource(), new Kturker.UnityBase.Tests.EditMode.FakeLog(), false);
            var languages = new List<string> { "en" };
            // cmpose:multiLang
            languages.AddRange(new[] { "tr", "fr", "es", "it" });
            // /cmpose:multiLang
            foreach (var language in languages)
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                foreach (var key in new[] { EconomyKeys.CurrencyAmount, EconomyKeys.CurrencyHave, EconomyKeys.CurrencyNotEnough })
                {
                    Assert.That(localizer.Get(key), Is.Not.EqualTo(key), $"{language}: {key} is missing");
                    var text = key == EconomyKeys.CurrencyNotEnough ? localizer.Get(key, name) : localizer.Get(key, 80, name);
                    var at = text.IndexOf(name, System.StringComparison.Ordinal);
                    Assert.That(at, Is.GreaterThanOrEqualTo(0), $"{language}: {key} names the currency: {text}");
                    var after = at + name.Length;
                    Assert.That(after == text.Length || !char.IsLetter(text[after]), Is.True, $"{language}: {key} adds no suffix: {text}");
                    Assert.That(at == 0 || !char.IsLetter(text[at - 1]), Is.True, $"{language}: {key} adds no prefix: {text}");
                }
                Assert.That(localizer.Get(EconomyKeys.CurrencyAmount, 80, name), Is.EqualTo("80 zorbs"), language);
                Assert.That(localizer.Get(EconomyKeys.Price, 80), Is.EqualTo("80"), language);
            }

            // A reward reads in the game's currency (the tables' name, else the file's: a game's tables name it)
            var config = EconomyConfig.Parse(EconomyTests.Json);
            var reward = new EconomyReward(80, null, 0);
            foreach (var language in languages)
            {
                localizer.SetLanguage(language);
                var named = new EconomyCurrency(config, localizer).Name;
                Assert.That(named, Is.Not.Empty, language);
                Assert.That(EconomyText.Reward(localizer, config, reward), Is.EqualTo($"80 {named}"), language);
            }
            // cmpose:multiLang
            localizer.SetLanguage("tr");
            Assert.That(localizer.Get(EconomyKeys.CurrencyHave, 100, "palamut"), Is.EqualTo("Sende: 100 palamut"));
            Assert.That(localizer.Get(EconomyKeys.CurrencyNotEnough, "Palamut"), Is.EqualTo("Palamut: yetmiyor"));
            // /cmpose:multiLang
        }

        private static HashSet<string> Keys(IReadOnlyList<StringFile> files)
        {
            var keys = new HashSet<string>();
            foreach (var file in files)
            {
                if (!JsonText.TryParse(file.Json, out var parsed) || !(parsed is Dictionary<string, object> table)) continue;
                foreach (var entry in table)
                {
                    if (entry.Value is string text && text.Length > 0) keys.Add(entry.Key);
                }
            }
            return keys;
        }
    }
}
