using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Features.Economy
{
    // The 7-day login bonus: one claim a local day. Claiming on consecutive days walks the streak 1..7 and day 7 is
    // followed by day 1; a missed day starts again at day 1. A claim saved in the future (the clock was turned back)
    // counts as today's, and the streak restarts at day 1 tomorrow. Nothing is ready while the economy is off, the game
    // has no login bonus (Configured false) or an AutoPlay run is on.
    public sealed class LoginBonus
    {
        private readonly Economy economy;
        private readonly Func<bool> autoPlay;
        private readonly Streak streak;

        public LoginBonus(Economy economy, IClock clock, Func<bool> autoPlay, TimeZoneInfo zone = null)
        {
            this.economy = economy;
            this.autoPlay = autoPlay;
            streak = new Streak(new StateValues(economy.Wallet), StateValues.Login, EconomyConfig.LoginDays, clock, zone);
        }

        // The economy is on and its file has a login bonus
        public bool Configured => economy.Enabled && economy.Config.Login != null;

        public IReadOnlyList<EconomyReward> Rewards => Configured ? economy.Config.Login : Array.Empty<EconomyReward>();

        // Today's streak day (1-7): the one to claim, or the one already claimed today
        public int Day => Configured ? streak.Day : 1;

        public bool ReadyToday => Configured && !autoPlay() && streak.ReadyToday;

        // Until the next local midnight (the next day's claim)
        public TimeSpan NextDayIn => streak.NextDayIn;

        // Pays today's day; null when it is not ready
        public EconomyReward Claim()
        {
            if (!ReadyToday) return null;
            var day = streak.Claim();
            if (day <= 0) return null;
            var reward = economy.Config.Login[day - 1];
            economy.Grant(reward.Coins, reward.Booster, reward.Count);
            return reward;
        }
    }
}
