using System;

namespace Kturker.UnityBase.Features.Economy
{
    // What game code uses: the coins and the boosters' counts. Off (Enabled false) without a GameEconomy.json: then
    // nothing is earned, spent or used and every count is 0.
    public interface IEconomy
    {
        bool Enabled { get; }
        int Coins { get; }

        // After the coins or a count change
        event Action Changed;

        void Earn(int coins);
        bool TrySpend(int coins);
        int Count(string boosterId);
        void Add(string boosterId, int count);

        // Takes one from the stock; false when there is none
        bool TryUse(string boosterId);

        // Null when the economy is off
        EconomyConfig Config { get; }
    }

    // IEconomy over a Wallet: only the config's boosters exist, and the first launch gets each one's start count once.
    // Also the custom extras' values (IEconomyExtrasStore), saved with the wallet.
    public sealed class Economy : IEconomy, IEconomyExtrasStore
    {
        public Economy(EconomyConfig config, IEconomyStore store)
        {
            Config = config;
            Wallet = new Wallet(store);
            if (config != null) GrantStart();
        }

        public bool Enabled => Config != null;
        public EconomyConfig Config { get; }
        public int Coins => Enabled ? Wallet.Coins : 0;

        internal Wallet Wallet { get; }

        public event Action Changed
        {
            add => Wallet.Changed += value;
            remove => Wallet.Changed -= value;
        }

        public void Earn(int coins)
        {
            if (Enabled) Wallet.Earn(coins);
        }

        public bool TrySpend(int coins) => Enabled && Wallet.TrySpend(coins);

        public int Count(string boosterId) => Known(boosterId) ? Wallet.Count(boosterId) : 0;

        public void Add(string boosterId, int count)
        {
            if (Known(boosterId)) Wallet.Add(boosterId, count);
        }

        public bool TryUse(string boosterId) => Known(boosterId) && Wallet.TryUse(boosterId);

        public IEconomyValues For(string extraId) => new ExtraValues(Wallet, extraId);

        // Coins and a booster the config knows (a reward); false when off
        internal void Grant(int coins, string booster, int count)
        {
            if (!Enabled) return;
            Wallet.Grant(coins, Known(booster) ? booster : null, count);
        }

        private bool Known(string boosterId) => Enabled && Config.Booster(boosterId) != null;

        private void GrantStart()
        {
            var state = Wallet.State;
            if (state.StartGranted) return;
            foreach (var booster in Config.Boosters)
            {
                if (booster.Start > 0) Wallet.AddQuietly(booster.Id, booster.Start);
            }
            state.StartGranted = true;
            Wallet.Commit();
        }
    }
}
