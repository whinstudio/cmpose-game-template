using System;

namespace Kturker.UnityBase.Features.Economy
{
    // Daily limits reset at local midnight: a day is the local calendar date as a day number (always above 0)
    public static class EconomyTime
    {
        private static readonly long MinMillis = DateTimeOffset.MinValue.AddDays(2).ToUnixTimeMilliseconds();
        private static readonly long MaxMillis = DateTimeOffset.MaxValue.AddDays(-2).ToUnixTimeMilliseconds();

        // The local day of a Unix time in milliseconds (zone null: the device's)
        public static long LocalDay(long millis, TimeZoneInfo zone)
        {
            var clamped = Math.Max(MinMillis, Math.Min(MaxMillis, millis));
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(clamped), zone ?? TimeZoneInfo.Local);
            return local.Date.Ticks / TimeSpan.TicksPerDay;
        }

        // From millis to the next local midnight (a daily limit's reset)
        public static TimeSpan UntilMidnight(long millis, TimeZoneInfo zone)
        {
            zone = zone ?? TimeZoneInfo.Local;
            var clamped = Math.Max(MinMillis, Math.Min(MaxMillis, millis));
            var now = DateTimeOffset.FromUnixTimeMilliseconds(clamped);
            var local = TimeZoneInfo.ConvertTime(now, zone);
            var next = local.Date.AddDays(1);
            var midnight = new DateTimeOffset(next, zone.GetUtcOffset(next));
            var left = midnight - now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }
}
