using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kturker.UnityBase.Core.Domain;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // How a booster is offered: picked before a run, or a HUD button during it
    public enum BoosterOffer
    {
        PreRun,
        InRun
    }

    // The game's money, designed per game: an English name (1-24 characters; the tables' economy.currency.name names it
    // in each language), its own colour and an optional white glyph
    public sealed class CurrencyConfig
    {
        public CurrencyConfig(string name, Color colour, string icon)
        {
            Name = name;
            Colour = colour;
            Icon = icon;
        }

        public string Name { get; }
        public Color Colour { get; }
        // A Resources path without extension (may be null: the fallback disc with the name's first letter)
        public string Icon { get; }
    }

    // What an extra is: one of the template's ready ones (its section in the file holds its values) or the game's own
    public enum ExtraKind
    {
        Supply,
        Wheel,
        Login,
        Custom
    }

    public sealed class ExtraConfig
    {
        public ExtraConfig(string id, ExtraKind kind)
        {
            Id = id;
            Kind = kind;
        }

        // Also names its strings: economy.extra.<id>.name (a custom extra's menu caption and window title)
        public string Id { get; }
        public ExtraKind Kind { get; }

        public string NameKey => $"economy.extra.{Id}.name";

        internal static readonly string[] Kinds = { "supply", "wheel", "login", "custom" };
    }

    public sealed class BoosterConfig
    {
        public BoosterConfig(string id, string icon, int price, BoosterOffer offer, int start)
        {
            Id = id;
            Icon = icon;
            Price = price;
            Offer = offer;
            Start = start;
        }

        // Also names its strings: economy.booster.<id>.name and .desc
        public string Id { get; }
        // A Resources path without extension (may be null)
        public string Icon { get; }
        public int Price { get; }
        public BoosterOffer Offer { get; }
        // How many the first launch gives
        public int Start { get; }

        public string NameKey => $"economy.booster.{Id}.name";
        public string DescriptionKey => $"economy.booster.{Id}.desc";
    }

    // Coins, a booster (null for none) or both: a login day, a wheel slice
    public class EconomyReward
    {
        public EconomyReward(int coins, string booster, int count)
        {
            Coins = coins;
            Booster = booster;
            Count = booster == null ? 0 : count;
        }

        public int Coins { get; }
        public string Booster { get; }
        public int Count { get; }
    }

    public sealed class WheelSlice : EconomyReward
    {
        public WheelSlice(int coins, string booster, int count, int weight) : base(coins, booster, count) => Weight = weight;

        public int Weight { get; }
    }

    public sealed class SupplyConfig
    {
        public SupplyConfig(int freeCoins, TimeSpan freeInterval, IReadOnlyList<int> adCoins)
        {
            FreeCoins = freeCoins;
            FreeInterval = freeInterval;
            AdCoins = adCoins;
        }

        public int FreeCoins { get; }
        public TimeSpan FreeInterval { get; }
        // The ad rows in the order they unlock
        public IReadOnlyList<int> AdCoins { get; }
    }

    public sealed class WheelConfig
    {
        public WheelConfig(int freeEvery, int adSpinsPerDay, IReadOnlyList<WheelSlice> slices)
        {
            FreeEvery = freeEvery;
            AdSpinsPerDay = adSpinsPerDay;
            Slices = slices;
        }

        // Runs (endless) or won levels per free spin
        public int FreeEvery { get; }
        public int AdSpinsPerDay { get; }
        public IReadOnlyList<WheelSlice> Slices { get; }
    }

    public sealed class WelcomeConfig
    {
        public WelcomeConfig(int coins, IReadOnlyDictionary<string, int> boosters)
        {
            Coins = coins;
            Boosters = boosters;
        }

        public int Coins { get; }
        public IReadOnlyDictionary<string, int> Boosters { get; }
    }

    // The game's economy, written by Compass to Resources/GameEconomy.json (version 1). A missing file is no economy;
    // so is a broken one: anything out of range, an unknown booster or a missing required part turns the whole economy
    // off rather than half of it. The currency, the boosters and the welcome bundle are always there. extras lists the
    // game's 0-3 extras (absent: none); supply, wheel and login are the ready ones' sections, each present exactly when
    // extras has its kind (null: that extra is off), and one that is present must be valid. A custom extra is the
    // game's own code over the template's helpers.
    public sealed class EconomyConfig
    {
        public const string ResourcePath = "GameEconomy";
        public const int SupportedVersion = 1;
        public const int LoginDays = 7;
        // Far above any planned value; keeps sums of rewards inside int
        private const int MaxAmount = 1000000;
        private const double MaxFreeHours = 24 * 7;
        public const int MaxExtras = 3;
        public const int MaxCurrencyName = 24;
        private static readonly Regex BoosterId = new Regex("^[a-z][a-z0-9_]{0,31}$");

        private static EconomyConfig current;
        private static bool loaded;

        private readonly Dictionary<string, BoosterConfig> byId = new Dictionary<string, BoosterConfig>(StringComparer.Ordinal);

        private EconomyConfig(CurrencyConfig currency, IReadOnlyList<BoosterConfig> boosters, IReadOnlyList<ExtraConfig> extras,
            SupplyConfig supply, WheelConfig wheel, IReadOnlyList<EconomyReward> login, WelcomeConfig welcome)
        {
            Currency = currency;
            Extras = extras;
            Boosters = boosters;
            Supply = supply;
            Wheel = wheel;
            Login = login;
            Welcome = welcome;
            foreach (var booster in boosters) byId[booster.Id] = booster;
        }

        public CurrencyConfig Currency { get; }
        public IReadOnlyList<BoosterConfig> Boosters { get; }
        // In the file's order; empty when the file has none
        public IReadOnlyList<ExtraConfig> Extras { get; }
        // Null: no supply
        public SupplyConfig Supply { get; }
        // Null: no wheel
        public WheelConfig Wheel { get; }
        // Day 1 to day 7; null: no login bonus
        public IReadOnlyList<EconomyReward> Login { get; }
        public WelcomeConfig Welcome { get; }

        // Null when the file is absent or broken; read once
        public static EconomyConfig Current
        {
            get
            {
                if (!loaded)
                {
                    var file = Resources.Load<TextAsset>(ResourcePath);
                    current = file == null ? null : Parse(file.text);
                    loaded = true;
                }
                return current;
            }
        }

        // Replaces Current (null: read the file again)
        internal static void ForTests(EconomyConfig config)
        {
            current = config;
            loaded = config != null;
        }

        public BoosterConfig Booster(string id) => id != null && byId.TryGetValue(id, out var booster) ? booster : null;

        // The extra with this id, null when the game has none
        public ExtraConfig Extra(string id)
        {
            foreach (var extra in Extras)
            {
                if (extra.Id == id) return extra;
            }
            return null;
        }

        // Null unless the text is a valid version 1 file
        public static EconomyConfig Parse(string json)
        {
            if (!JsonText.TryParse(json, out var parsed) || !(parsed is Dictionary<string, object> root)) return null;
            if (!Int(root, "version", 1, int.MaxValue, out var version) || version != SupportedVersion) return null;
            var currency = ReadCurrency(root);
            if (currency == null) return null;
            var boosters = ReadBoosters(root);
            if (boosters == null) return null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var booster in boosters) ids.Add(booster.Id);
            var welcome = ReadWelcome(Section(root, "welcome"), ids);
            if (welcome == null) return null;
            SupplyConfig supply = null;
            WheelConfig wheel = null;
            List<EconomyReward> login = null;
            if (root.ContainsKey("supply") && (supply = ReadSupply(Section(root, "supply"))) == null) return null;
            if (root.ContainsKey("wheel") && (wheel = ReadWheel(Section(root, "wheel"), ids)) == null) return null;
            if (root.ContainsKey("login") && (login = ReadLogin(root, ids)) == null) return null;
            var extras = ReadExtras(root);
            if (extras == null || !Matches(extras, ExtraKind.Supply, supply != null) || !Matches(extras, ExtraKind.Wheel, wheel != null) ||
                !Matches(extras, ExtraKind.Login, login != null)) return null;
            return new EconomyConfig(currency, boosters, extras, supply, wheel, login, welcome);
        }

        // A name (1-24 characters, not blank), a colour "#RRGGBB" and an optional icon path
        private static CurrencyConfig ReadCurrency(Dictionary<string, object> root)
        {
            var currency = Section(root, "currency");
            if (currency == null || !OptionalText(currency, "icon", out var icon)) return null;
            var name = Text(currency, "name")?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaxCurrencyName) return null;
            var hex = Text(currency, "color");
            if (hex == null || hex.Length != 7 || hex[0] != '#' || !ColorUtility.TryParseHtmlString(hex, out var colour)) return null;
            return new CurrencyConfig(name, colour, icon);
        }

        // Absent: none. Up to MaxExtras, unique ids, a known kind each and a ready kind at most once
        private static List<ExtraConfig> ReadExtras(Dictionary<string, object> root)
        {
            var extras = new List<ExtraConfig>();
            if (!root.TryGetValue("extras", out var value)) return extras;
            if (!(value is List<object> items) || items.Count > MaxExtras) return null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> entry)) return null;
                if (!(Text(entry, "id") is string id) || !BoosterId.IsMatch(id) || !seen.Add(id)) return null;
                var kind = Array.IndexOf(ExtraConfig.Kinds, Text(entry, "kind"));
                if (kind < 0) return null;
                if ((ExtraKind)kind != ExtraKind.Custom && extras.Exists(e => e.Kind == (ExtraKind)kind)) return null;
                extras.Add(new ExtraConfig(id, (ExtraKind)kind));
            }
            return extras;
        }

        // A ready extra is listed exactly when its section is there
        private static bool Matches(List<ExtraConfig> extras, ExtraKind kind, bool section) => extras.Exists(e => e.Kind == kind) == section;

        private static List<BoosterConfig> ReadBoosters(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("boosters", out var value) || !(value is List<object> items)) return null;
            var boosters = new List<BoosterConfig>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> entry)) return null;
                if (!(Text(entry, "id") is string id) || !BoosterId.IsMatch(id) || !seen.Add(id)) return null;
                if (!OptionalText(entry, "icon", out var icon)) return null;
                if (!Int(entry, "price", 1, MaxAmount, out var price) || !Int(entry, "start", 0, MaxAmount, out var start)) return null;
                BoosterOffer offer;
                switch (Text(entry, "offer"))
                {
                    case "preRun": offer = BoosterOffer.PreRun; break;
                    case "inRun": offer = BoosterOffer.InRun; break;
                    default: return null;
                }
                boosters.Add(new BoosterConfig(id, icon, price, offer, start));
            }
            return boosters;
        }

        private static SupplyConfig ReadSupply(Dictionary<string, object> supply)
        {
            if (supply == null || !Int(supply, "freeCoins", 0, MaxAmount, out var freeCoins)) return null;
            if (!supply.TryGetValue("freeHours", out var hoursValue) || !(hoursValue is double hours) || hours <= 0d ||
                hours > MaxFreeHours) return null;
            if (!supply.TryGetValue("adCoins", out var rowsValue) || !(rowsValue is List<object> rows)) return null;
            var adCoins = new List<int>();
            foreach (var row in rows)
            {
                if (!Whole(row, 1, MaxAmount, out var coins)) return null;
                adCoins.Add(coins);
            }
            return new SupplyConfig(freeCoins, TimeSpan.FromHours(hours), adCoins);
        }

        private static WheelConfig ReadWheel(Dictionary<string, object> wheel, HashSet<string> ids)
        {
            if (wheel == null || !Int(wheel, "freeEvery", 1, 1000, out var freeEvery) ||
                !Int(wheel, "adSpinsPerDay", 0, 100, out var adSpins)) return null;
            if (!wheel.TryGetValue("slices", out var value) || !(value is List<object> items) || items.Count == 0) return null;
            var slices = new List<WheelSlice>();
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> entry) || !Int(entry, "weight", 1, MaxAmount, out var weight)) return null;
                var reward = Reward(entry, ids);
                if (reward == null || (reward.Coins == 0 && reward.Booster == null)) return null;
                slices.Add(new WheelSlice(reward.Coins, reward.Booster, reward.Count, weight));
            }
            return new WheelConfig(freeEvery, adSpins, slices);
        }

        private static List<EconomyReward> ReadLogin(Dictionary<string, object> root, HashSet<string> ids)
        {
            if (!root.TryGetValue("login", out var value) || !(value is List<object> items) || items.Count != LoginDays) return null;
            var days = new List<EconomyReward>();
            foreach (var item in items)
            {
                var reward = item is Dictionary<string, object> entry ? Reward(entry, ids) : null;
                if (reward == null) return null;
                days.Add(reward);
            }
            return days;
        }

        private static WelcomeConfig ReadWelcome(Dictionary<string, object> welcome, HashSet<string> ids)
        {
            if (welcome == null || !Int(welcome, "coins", 0, MaxAmount, out var coins)) return null;
            var boosters = new Dictionary<string, int>(StringComparer.Ordinal);
            if (welcome.TryGetValue("boosters", out var value))
            {
                if (!(value is Dictionary<string, object> entries)) return null;
                foreach (var entry in entries)
                {
                    if (!ids.Contains(entry.Key) || !Whole(entry.Value, 0, MaxAmount, out var count)) return null;
                    boosters[entry.Key] = count;
                }
            }
            return new WelcomeConfig(coins, boosters);
        }

        // coins (default 0) and an optional booster with its count (default 1)
        private static EconomyReward Reward(Dictionary<string, object> entry, HashSet<string> ids)
        {
            var coins = 0;
            if (entry.ContainsKey("coins") && !Int(entry, "coins", 0, MaxAmount, out coins)) return null;
            if (!entry.TryGetValue("booster", out var boosterValue)) return new EconomyReward(coins, null, 0);
            if (!(boosterValue is string booster) || !ids.Contains(booster)) return null;
            var count = 1;
            if (entry.ContainsKey("count") && !Int(entry, "count", 1, MaxAmount, out count)) return null;
            return new EconomyReward(coins, booster, count);
        }

        private static Dictionary<string, object> Section(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;

        private static string Text(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out var value) ? value as string : null;

        // Absent or a string
        private static bool OptionalText(Dictionary<string, object> parent, string key, out string text)
        {
            text = null;
            if (!parent.TryGetValue(key, out var value)) return true;
            text = value as string;
            return text != null;
        }

        private static bool Int(Dictionary<string, object> parent, string key, int min, int max, out int result)
        {
            result = 0;
            return parent.TryGetValue(key, out var value) && Whole(value, min, max, out result);
        }

        // A whole JSON number in [min, max]
        private static bool Whole(object value, int min, int max, out int result)
        {
            result = 0;
            if (!(value is double number) || number < min || number > max || Math.Floor(number) != number) return false;
            result = (int)number;
            return true;
        }
    }
}
