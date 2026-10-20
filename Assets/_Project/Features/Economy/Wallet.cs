using System;

namespace Kturker.UnityBase.Features.Economy
{
    // Coins and booster counts over an IEconomyStore: every change is saved at once and raises Changed. It knows no
    // config; Economy decides which boosters exist.
    public sealed class Wallet
    {
        private readonly IEconomyStore store;
        private EconomyState state;

        public Wallet(IEconomyStore store) => this.store = store;

        public event Action Changed;

        public int Coins => State.Coins;

        // Loaded on first use; a missing save is an empty wallet
        internal EconomyState State
        {
            get
            {
                if (state != null) return state;
                state = store.Load() ?? new EconomyState();
                state.Normalize();
                return state;
            }
        }

        public void Earn(int coins)
        {
            if (coins <= 0) return;
            State.Coins = Sum(State.Coins, coins);
            Commit();
        }

        public bool TrySpend(int coins)
        {
            if (coins < 0 || State.Coins < coins) return false;
            if (coins == 0) return true;
            State.Coins -= coins;
            Commit();
            return true;
        }

        public int Count(string id) => Find(id)?.Count ?? 0;

        public void Add(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            AddQuietly(id, count);
            Commit();
        }

        public bool TryUse(string id)
        {
            var stock = Find(id);
            if (stock == null || stock.Count <= 0) return false;
            stock.Count--;
            Commit();
            return true;
        }

        // Coins and a booster in one change (rewards)
        internal void Grant(int coins, string booster, int count)
        {
            if (coins > 0) State.Coins = Sum(State.Coins, coins);
            if (!string.IsNullOrEmpty(booster) && count > 0) AddQuietly(booster, count);
            Commit();
        }

        internal void AddQuietly(string id, int count)
        {
            var stock = Find(id);
            if (stock == null) State.Boosters.Add(new BoosterStock(id, count));
            else stock.Count = Sum(stock.Count, count);
        }

        // Timers and counters: saved, but the coins and counts did not change
        internal void Save() => store.Save(State);

        internal void Commit()
        {
            store.Save(State);
            Changed?.Invoke();
        }

        private BoosterStock Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var stock in State.Boosters)
            {
                if (stock.Id == id) return stock;
            }
            return null;
        }

        private static int Sum(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
    }
}
