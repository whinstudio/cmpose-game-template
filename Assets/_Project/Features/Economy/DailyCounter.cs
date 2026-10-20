using System;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Features.Economy
{
    // Uses a day up to Limit (ad spins, scratch cards), reset at local midnight. A day saved in the future (the clock
    // was turned back) becomes today with its uses kept, so turning the clock back never gives more. Keeps the uses
    // under key and their day under DayKey(key).
    public sealed class DailyCounter
    {
        private readonly IEconomyValues values;
        private readonly string key;
        private readonly IClock clock;
        private readonly TimeZoneInfo zone;

        public DailyCounter(IEconomyValues values, string key, int limit, IClock clock, TimeZoneInfo zone = null)
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.zone = zone ?? TimeZoneInfo.Local;
            Limit = Math.Max(0, limit);
        }

        public int Limit { get; }

        // Today's uses
        public int Used
        {
            get
            {
                var today = Today;
                var day = values.Get(DayKey(key));
                if (day != today)
                {
                    if (day < today) values.Set(key, 0);
                    values.Set(DayKey(key), today);
                }
                return (int)Math.Max(0L, values.Get(key));
            }
        }

        public int Left => Math.Max(0, Limit - Used);

        // The local day now (EconomyTime.LocalDay)
        public long Today => EconomyTime.LocalDay(clock.NowMillis, zone);

        // Until the uses reset
        public TimeSpan ResetsIn => EconomyTime.UntilMidnight(clock.NowMillis, zone);

        // Counts one use; false when none is left today
        public bool TryUse()
        {
            var used = Used;
            if (used >= Limit) return false;
            values.Set(key, used + 1);
            return true;
        }

        public static string DayKey(string key) => key + ".day";
    }
}
