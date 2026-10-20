using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // Where the wallet lives: MemoryEconomyStore, or with the saveSystem flag SavedEconomyStore (slot 'wallet').
    // Load returns null when nothing usable is saved.
    public interface IEconomyStore
    {
        EconomyState Load();
        void Save(EconomyState state);
    }

    // The wallet and every timer of the economy, in one save slot. Times are Unix milliseconds, days are local
    // calendar days (EconomyTime.LocalDay, 0: never).
    [Serializable]
    public sealed class EconomyState
    {
        [SerializeField] private int coins;
        [SerializeField] private List<BoosterStock> boosters = new List<BoosterStock>();
        [SerializeField] private bool startGranted;
        [SerializeField] private long supplyFreeAt;
        [SerializeField] private int supplyAdRow;
        [SerializeField] private long supplyAdDay;
        [SerializeField] private int wheelProgress;
        [SerializeField] private int wheelAdSpins;
        [SerializeField] private long wheelAdDay;
        [SerializeField] private int loginDay;
        [SerializeField] private long loginClaimedDay;
        [SerializeField] private bool welcomeGiven;
        [SerializeField] private List<ExtraValue> extraValues = new List<ExtraValue>();

        public int Coins { get => coins; set => coins = value; }
        public bool StartGranted { get => startGranted; set => startGranted = value; }
        // When the free supply was last claimed (its timer's start)
        public long SupplyFreeAt { get => supplyFreeAt; set => supplyFreeAt = value; }
        // The next ad row of the supply ladder on SupplyAdDay
        public int SupplyAdRow { get => supplyAdRow; set => supplyAdRow = value; }
        public long SupplyAdDay { get => supplyAdDay; set => supplyAdDay = value; }
        public int WheelProgress { get => wheelProgress; set => wheelProgress = value; }
        // Ad spins used on WheelAdDay
        public int WheelAdSpins { get => wheelAdSpins; set => wheelAdSpins = value; }
        public long WheelAdDay { get => wheelAdDay; set => wheelAdDay = value; }
        // The streak day claimed on LoginClaimedDay (1-7; 0: none)
        public int LoginDay { get => loginDay; set => loginDay = value; }
        public long LoginClaimedDay { get => loginClaimedDay; set => loginClaimedDay = value; }
        public bool WelcomeGiven { get => welcomeGiven; set => welcomeGiven = value; }

        public List<BoosterStock> Boosters
        {
            get
            {
                if (boosters == null) boosters = new List<BoosterStock>();
                return boosters;
            }
        }

        // The custom extras' values (IEconomyExtrasStore)
        public List<ExtraValue> ExtraValues
        {
            get
            {
                if (extraValues == null) extraValues = new List<ExtraValue>();
                return extraValues;
            }
        }

        // A damaged or edited save never yields negative coins, counts or counters
        public void Normalize()
        {
            coins = Math.Max(0, coins);
            Boosters.RemoveAll(stock => stock == null || string.IsNullOrEmpty(stock.Id));
            foreach (var stock in Boosters) stock.Count = Math.Max(0, stock.Count);
            supplyAdRow = Math.Max(0, supplyAdRow);
            wheelProgress = Math.Max(0, wheelProgress);
            wheelAdSpins = Math.Max(0, wheelAdSpins);
            loginDay = Mathf.Clamp(loginDay, 0, EconomyConfig.LoginDays);
            ExtraValues.RemoveAll(value => value == null || string.IsNullOrEmpty(value.Id) || value.Key == null);
        }
    }

    // One value of a custom extra: a number, a text or both under (id, key)
    [Serializable]
    public sealed class ExtraValue
    {
        [SerializeField] private string id;
        [SerializeField] private string key;
        [SerializeField] private long number;
        [SerializeField] private string text;

        public ExtraValue()
        {
        }

        public ExtraValue(string id, string key)
        {
            this.id = id;
            this.key = key;
        }

        public string Id => id;
        public string Key => key;
        public long Number { get => number; set => number = value; }
        // JsonUtility writes a missing text as ""; null and "" both read as null
        public string Text { get => string.IsNullOrEmpty(text) ? null : text; set => text = value; }
    }

    [Serializable]
    public sealed class BoosterStock
    {
        [SerializeField] private string id;
        [SerializeField] private int count;

        public BoosterStock()
        {
        }

        public BoosterStock(string id, int count)
        {
            this.id = id;
            this.count = count;
        }

        public string Id => id;
        public int Count { get => count; set => count = value; }
    }
}
