using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Features.Economy
{
    // The daily supply: a free row every FreeInterval and a ladder of ad rows that unlock in order and reset at local
    // midnight. A timer or day saved in the future (the clock was turned back) restarts from now and never pays twice.
    // Nothing is ready while the economy is off, the game has no supply (Configured false) or an AutoPlay run is on.
    public sealed class Supply
    {
        private readonly Economy economy;
        private readonly IEconomyAds ads;
        private readonly Func<bool> autoPlay;
        private readonly Cooldown free;
        private readonly DailyCounter adRows;
        private bool watching;

        public Supply(Economy economy, IEconomyAds ads, IClock clock, Func<bool> autoPlay, TimeZoneInfo zone = null)
        {
            this.economy = economy;
            this.ads = ads;
            this.autoPlay = autoPlay;
            var config = economy.Config?.Supply;
            var values = new StateValues(economy.Wallet);
            free = new Cooldown(values, StateValues.SupplyFree, config?.FreeInterval ?? TimeSpan.Zero, clock);
            adRows = new DailyCounter(values, StateValues.SupplyAds, config?.AdCoins.Count ?? 0, clock, zone);
        }

        // The economy is on and its file has a supply
        public bool Configured => economy.Enabled && economy.Config.Supply != null;

        public bool FreeReady => Open && free.Ready;

        // Zero once ready (and while the supply is off)
        public TimeSpan FreeReadyIn => Configured ? free.Remaining : TimeSpan.Zero;

        // The ad row that can be claimed next (0 first), -1 when today's ladder is done or nothing is open
        public int NextAdRow
        {
            get
            {
                if (!Open) return -1;
                var row = adRows.Used;
                return row < adRows.Limit ? row : -1;
            }
        }

        // Today's ad rows claimed so far (0 while nothing is open)
        public int AdRowsClaimed => Open ? Math.Min(adRows.Used, adRows.Limit) : 0;

        // The next ad row can be offered now
        public bool AdRowReady => NextAdRow >= 0 && ads.RewardedAvailable && !watching;

        private bool Open => Configured && !autoPlay();

        public bool ClaimFree()
        {
            if (!FreeReady || !free.TryStart()) return false;
            economy.Grant(economy.Config.Supply.FreeCoins, null, 0);
            return true;
        }

        // Shows a rewarded ad for the row (it must be NextAdRow) and pays it only when the ad was watched
        public async UniTask<bool> ClaimAdRowAsync(int row, CancellationToken ct)
        {
            if (row != NextAdRow || !AdRowReady) return false;
            watching = true;
            bool earned;
            try
            {
                earned = await ads.ShowRewardedAsync(ct);
            }
            finally
            {
                watching = false;
            }
            // Midnight may have passed while the ad was on screen: the row must still be the next one
            if (!earned || row != NextAdRow || !adRows.TryUse()) return false;
            economy.Grant(economy.Config.Supply.AdCoins[row], null, 0);
            return true;
        }
    }
}
