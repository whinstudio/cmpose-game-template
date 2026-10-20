using System;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Features.Economy
{
    // A wait between two uses (a free reward every few hours): ready until the first Start, then Interval after each
    // one. A start saved in the future (the clock was turned back) restarts the wait from now, so turning the clock
    // back never pays twice. Keeps its start under key.
    public sealed class Cooldown
    {
        private readonly IEconomyValues values;
        private readonly string key;
        private readonly IClock clock;

        public Cooldown(IEconomyValues values, string key, TimeSpan interval, IClock clock)
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Interval = interval;
        }

        public TimeSpan Interval { get; }

        // Zero once ready
        public TimeSpan Remaining
        {
            get
            {
                var started = values.Get(key);
                if (started == 0) return TimeSpan.Zero;
                var now = clock.NowMillis;
                if (started > now)
                {
                    values.Set(key, now);
                    started = now;
                }
                var left = Interval - TimeSpan.FromMilliseconds(now - started);
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public bool Ready => Remaining <= TimeSpan.Zero;

        // Starts the next wait; false (nothing changes) while waiting
        public bool TryStart()
        {
            if (!Ready) return false;
            values.Set(key, clock.NowMillis);
            return true;
        }
    }
}
