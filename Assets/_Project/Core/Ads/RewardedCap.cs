using System;
using System.Globalization;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Update;

namespace Kturker.UnityBase.Core.Ads
{
    // The daily cap on rewarded ads, shared by everything that shows one (Continue, the economy's ad rows and spins).
    // Only an ad watched to the end counts. The count is kept in IPreferences with the local date it belongs to, so it
    // starts over at local midnight. A count saved under a later date (the clock was turned back) still counts today
    // and keeps that date, so turning the clock back never frees more ads.
    public sealed class RewardedCap
    {
        public const string DateKey = "ads.rewarded.date";
        public const string CountKey = "ads.rewarded.count";
        private static readonly long MinMillis = DateTimeOffset.MinValue.AddDays(2).ToUnixTimeMilliseconds();
        private static readonly long MaxMillis = DateTimeOffset.MaxValue.AddDays(-2).ToUnixTimeMilliseconds();

        private readonly IPreferences preferences;
        private readonly IClock clock;
        private readonly int perDay;
        private readonly TimeZoneInfo zone;

        public RewardedCap(IPreferences preferences, IClock clock, int perDay, TimeZoneInfo zone = null)
        {
            this.preferences = preferences;
            this.clock = clock;
            this.perDay = Math.Max(0, perDay);
            this.zone = zone ?? TimeZoneInfo.Local;
        }

        public int CountToday => SavedCounts(Today()) ? Math.Max(0, preferences.GetInt(CountKey, 0)) : 0;

        public bool Reached => CountToday >= perDay;

        // After an ad was watched to the end
        public void Count()
        {
            var today = Today();
            var counts = SavedCounts(today);
            var count = (counts ? Math.Max(0, preferences.GetInt(CountKey, 0)) : 0) + 1;
            if (!counts) preferences.SetString(DateKey, today);
            preferences.SetInt(CountKey, count);
            preferences.Save();
        }

        // yyyy-MM-dd strings sort like the dates they name
        private bool SavedCounts(string today)
        {
            var saved = preferences.GetString(DateKey, null);
            return saved != null && string.CompareOrdinal(saved, today) >= 0;
        }

        private string Today()
        {
            var millis = Math.Max(MinMillis, Math.Min(MaxMillis, clock.NowMillis));
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(millis), zone);
            return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
