using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Purchases;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // purchases flag: pays what a bought shop row holds (coins, boosters), exactly once per store transaction. A
    // purchase arrives by ProductPurchased and again with every customer info (TransactionsUpdated, a restore too); a
    // purchase the app missed (closed while it ran) arrives with the next customer info. The wallet's ledger
    // (EconomyState.GrantedTransactions) holds every paid transaction id, saved with the coins in one commit.
    // A reinstall starts a new ledger, so a customer info only pays transactions made since this install's first launch
    // (EconomyState.FirstLaunchUtcTicks): the history is never paid twice. A live ProductPurchased always pays: it is a
    // purchase made now, whatever the device clock says. Remove Ads (pays nothing) is never written down.
    public sealed class ShopGrants : IInitializable, IDisposable
    {
        private readonly Economy economy;
        private readonly IPurchases purchases;
        private readonly EconomyShopCatalog catalog;
        private readonly Func<DateTime> utcNow;
        private bool listening;

        public ShopGrants(Economy economy, IPurchases purchases, EconomyShopCatalog catalog, Func<DateTime> utcNow)
        {
            this.economy = economy;
            this.purchases = purchases;
            this.catalog = catalog;
            this.utcNow = utcNow;
        }

        public void Initialize()
        {
            if (listening) return;
            var state = economy.Wallet.State;
            if (state.FirstLaunchUtcTicks <= 0)
            {
                state.FirstLaunchUtcTicks = utcNow().Ticks;
                economy.Wallet.Save();
            }
            purchases.ProductPurchased += OnProductPurchased;
            purchases.TransactionsUpdated += OnTransactionsUpdated;
            listening = true;
            OnTransactionsUpdated(purchases.Transactions);
        }

        public void Dispose()
        {
            if (!listening) return;
            purchases.ProductPurchased -= OnProductPurchased;
            purchases.TransactionsUpdated -= OnTransactionsUpdated;
            listening = false;
        }

        private void OnProductPurchased(StoreTransaction transaction)
        {
            if (transaction != null) Grant(new[] { transaction }, live: true);
        }

        private void OnTransactionsUpdated(IReadOnlyList<StoreTransaction> transactions)
        {
            if (transactions != null) Grant(transactions, live: false);
        }

        private void Grant(IReadOnlyList<StoreTransaction> transactions, bool live)
        {
            if (!economy.Enabled) return;
            var state = economy.Wallet.State;
            var coins = 0;
            var paid = false;
            foreach (var transaction in transactions)
            {
                if (transaction == null || string.IsNullOrEmpty(transaction.Id) || state.GrantedTransactions.Contains(transaction.Id)) continue;
                var row = catalog.Row(transaction.ProductId);
                if (row == null || !row.PaysSomething) continue;
                if (!live && transaction.PurchasedUtc.Ticks < state.FirstLaunchUtcTicks) continue;
                state.GrantedTransactions.Add(transaction.Id);
                coins = Sum(coins, row.Coins);
                foreach (var booster in row.Boosters)
                {
                    if (booster.Value > 0 && economy.Config.Booster(booster.Key) != null) economy.Wallet.AddQuietly(booster.Key, booster.Value);
                }
                paid = true;
            }
            // One commit for the ledger, the coins and the boosters (Grant commits even with no coins)
            if (paid) economy.Grant(coins, null, 0);
        }

        private static int Sum(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
    }
}
