using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Update;
using Kturker.UnityBase.Features.Shell.GameShell;

namespace Kturker.UnityBase.Features.Economy
{
    // The lucky wheel: a free spin every FreeEvery runs (endless) or won levels (levels), and AdSpinsPerDay spins for a
    // watched ad a day (reset at local midnight). A slice is picked by weight and paid at once. Nothing is ready (and
    // runs are not counted) while the economy is off, the game has no wheel (Configured false) or an AutoPlay run is on.
    public sealed class Wheel
    {
        private readonly Economy economy;
        private readonly IEconomyAds ads;
        private readonly Func<bool> autoPlay;
        private readonly DailyCounter adSpins;
        private bool watching;

        public Wheel(Economy economy, IEconomyAds ads, IClock clock, Func<bool> autoPlay, TimeZoneInfo zone = null)
        {
            this.economy = economy;
            this.ads = ads;
            this.autoPlay = autoPlay;
            adSpins = new DailyCounter(new StateValues(economy.Wallet), StateValues.WheelAds, economy.Config?.Wheel?.AdSpinsPerDay ?? 0,
                clock, zone);
        }

        // The economy is on and its file has a wheel
        public bool Configured => economy.Enabled && economy.Config.Wheel != null;

        public IReadOnlyList<WheelSlice> Slices => Configured ? economy.Config.Wheel.Slices : Array.Empty<WheelSlice>();
        public int FreeEvery => Configured ? economy.Config.Wheel.FreeEvery : 0;

        // 0..FreeEvery
        public int Progress => Configured ? Math.Min(State.WheelProgress, FreeEvery) : 0;

        public bool FreeSpinReady => Open && Progress >= FreeEvery;

        public int AdSpinsLeft => Open ? adSpins.Left : 0;

        public bool AdSpinReady => AdSpinsLeft > 0 && ads.RewardedAvailable && !watching;

        private bool Open => Configured && !autoPlay();
        private EconomyState State => economy.Wallet.State;

        // Endless: every run moves the wheel on; levels: only a won level
        public void RunEnded(bool won, ShellMode mode)
        {
            if (!Open || (mode == ShellMode.Levels && !won) || State.WheelProgress >= FreeEvery) return;
            State.WheelProgress++;
            economy.Wallet.Commit();
        }

        // The free spin; null when it is not ready
        public WheelSlice Spin(Random random)
        {
            if (!FreeSpinReady) return null;
            State.WheelProgress = 0;
            return Pay(random);
        }

        // A spin for a watched rewarded ad; null when none is left, the ad was not watched, or the day changed
        public async UniTask<WheelSlice> SpinWithAdAsync(Random random, CancellationToken ct)
        {
            if (!AdSpinReady) return null;
            var day = adSpins.Today;
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
            // Midnight may have passed while the ad was on screen: the spin must still be today's
            if (!earned || AdSpinsLeft <= 0 || adSpins.Today != day || !adSpins.TryUse()) return null;
            return Pay(random);
        }

        // A slice by weight (every weight is at least 1)
        internal static WheelSlice Pick(IReadOnlyList<WheelSlice> slices, Random random) =>
            WeightedPick.Pick(slices, slice => slice.Weight, random);

        private WheelSlice Pay(Random random)
        {
            var slice = Pick(economy.Config.Wheel.Slices, random);
            economy.Grant(slice.Coins, slice.Booster, slice.Count);
            return slice;
        }
    }
}
