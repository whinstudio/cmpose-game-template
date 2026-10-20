using System;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Features.Economy
{
    // A run of consecutive local days with one claim each (a login bonus): a claim on the next day moves it on, a
    // missed day starts it again at 1, and with a Cycle (7) the day after the last is 1 again (0: it keeps growing).
    // A claim saved in the future (the clock was turned back) counts as today's and the streak restarts at 1 the next
    // day, so turning the clock back never pays twice. Keeps the streak day under key and its local day under
    // DayKey(key).
    public sealed class Streak
    {
        private readonly IEconomyValues values;
        private readonly string key;
        private readonly IClock clock;
        private readonly TimeZoneInfo zone;

        public Streak(IEconomyValues values, string key, int cycle, IClock clock, TimeZoneInfo zone = null)
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.zone = zone ?? TimeZoneInfo.Local;
            Cycle = Math.Max(0, cycle);
        }

        public int Cycle { get; }

        // Today's streak day (1 first): the one to claim, or the one claimed today
        public int Day
        {
            get
            {
                var next = Next();
                return next > 0 ? next : (int)Math.Max(1L, values.Get(key));
            }
        }

        public bool ReadyToday => Next() > 0;

        // Until the next local midnight (the next day's claim)
        public TimeSpan NextDayIn => EconomyTime.UntilMidnight(clock.NowMillis, zone);

        // Claims today: the streak day claimed, 0 when today is already claimed
        public int Claim()
        {
            var day = Next();
            if (day <= 0) return 0;
            values.Set(key, day);
            values.Set(DayKey(key), EconomyTime.LocalDay(clock.NowMillis, zone));
            return day;
        }

        public static string DayKey(string key) => key + ".day";

        // The day to claim today, -1 when today is claimed
        private int Next()
        {
            var today = EconomyTime.LocalDay(clock.NowMillis, zone);
            var claimed = values.Get(DayKey(key));
            if (claimed > today)
            {
                values.Set(DayKey(key), today);
                values.Set(key, 0);
                claimed = today;
            }
            if (claimed == today) return -1;
            var streak = values.Get(key);
            if (claimed == today - 1 && streak >= 1) return Cycle > 0 ? (int)(streak % Cycle) + 1 : (int)Math.Min(int.MaxValue, streak + 1);
            return 1;
        }
    }
}
