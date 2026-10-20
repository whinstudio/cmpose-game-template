using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.Features.Economy
{
    // Small values an extra keeps between sessions, by key: numbers (times in Unix milliseconds, days, counts) and
    // short texts (a little JSON). Every Set is saved at once, with the wallet (the 'wallet' slot with the saveSystem
    // flag, else in memory until the app closes).
    public interface IEconomyValues
    {
        // 0 when never set
        long Get(string key);
        void Set(string key, long value);
        // Null when never set
        string GetText(string key);
        void SetText(string key, string value);
    }

    // Each extra's own values: For(id) never sees another id's keys. Economy is the root scope's one.
    public interface IEconomyExtrasStore
    {
        IEconomyValues For(string extraId);
    }

    // One extra's values in the wallet's state
    internal sealed class ExtraValues : IEconomyValues
    {
        private readonly Wallet wallet;
        private readonly string id;

        public ExtraValues(Wallet wallet, string id)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public long Get(string key) => Find(key)?.Number ?? 0L;

        public void Set(string key, long value)
        {
            Entry(key).Number = value;
            wallet.Save();
        }

        public string GetText(string key) => Find(key)?.Text;

        public void SetText(string key, string value)
        {
            Entry(key).Text = value;
            wallet.Save();
        }

        private ExtraValue Find(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            foreach (var value in wallet.State.ExtraValues)
            {
                if (value.Id == id && value.Key == key) return value;
            }
            return null;
        }

        private ExtraValue Entry(string key)
        {
            var value = Find(key);
            if (value != null) return value;
            value = new ExtraValue(id, key);
            wallet.State.ExtraValues.Add(value);
            return value;
        }
    }

    // The ready extras' fields of the wallet's state under the helpers' keys (Supply, Wheel and LoginBonus keep their
    // own fields, so their saves stay as they were)
    internal sealed class StateValues : IEconomyValues
    {
        public const string SupplyFree = "supply.free";
        public const string SupplyAds = "supply.ads";
        public const string WheelAds = "wheel.ads";
        public const string Login = "login";

        private readonly Wallet wallet;
        private readonly Dictionary<string, Func<EconomyState, long>> getters;
        private readonly Dictionary<string, Action<EconomyState, long>> setters;

        public StateValues(Wallet wallet)
        {
            this.wallet = wallet;
            getters = new Dictionary<string, Func<EconomyState, long>>(StringComparer.Ordinal)
            {
                [SupplyFree] = s => s.SupplyFreeAt,
                [SupplyAds] = s => s.SupplyAdRow,
                [DailyCounter.DayKey(SupplyAds)] = s => s.SupplyAdDay,
                [WheelAds] = s => s.WheelAdSpins,
                [DailyCounter.DayKey(WheelAds)] = s => s.WheelAdDay,
                [Login] = s => s.LoginDay,
                [Streak.DayKey(Login)] = s => s.LoginClaimedDay
            };
            setters = new Dictionary<string, Action<EconomyState, long>>(StringComparer.Ordinal)
            {
                [SupplyFree] = (s, v) => s.SupplyFreeAt = v,
                [SupplyAds] = (s, v) => s.SupplyAdRow = (int)v,
                [DailyCounter.DayKey(SupplyAds)] = (s, v) => s.SupplyAdDay = v,
                [WheelAds] = (s, v) => s.WheelAdSpins = (int)v,
                [DailyCounter.DayKey(WheelAds)] = (s, v) => s.WheelAdDay = v,
                [Login] = (s, v) => s.LoginDay = (int)v,
                [Streak.DayKey(Login)] = (s, v) => s.LoginClaimedDay = v
            };
        }

        public long Get(string key) => getters[key](wallet.State);

        public void Set(string key, long value)
        {
            setters[key](wallet.State, value);
            wallet.Save();
        }

        public string GetText(string key) => throw new NotSupportedException(key);

        public void SetText(string key, string value) => throw new NotSupportedException(key);
    }
}
