using System;
using System.Collections.Generic;
using System.Globalization;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Features.Economy
{
    // The texts the windows write: a countdown, a reward, a share in percent
    public static class EconomyText
    {
        // "2:59:41", whole seconds rounded up (a timer that just started reads its full length)
        public static string Timer(TimeSpan left)
        {
            var seconds = left > TimeSpan.Zero ? (long)Math.Ceiling(left.TotalSeconds) : 0L;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60);
        }

        // "15 acorns", "1 Bomb" or "200 acorns + 1 Bomb" (the amount in the game's currency)
        public static string Reward(ILocalizer localizer, EconomyConfig config, EconomyReward reward)
        {
            var parts = new List<string>();
            if (reward.Coins > 0) parts.Add(new EconomyCurrency(config, localizer).Amount(reward.Coins));
            if (reward.Booster != null) parts.Add(localizer.Get(EconomyKeys.RewardBooster, reward.Count, BoosterName(localizer, config, reward.Booster)));
            return string.Join(" + ", parts);
        }

        public static string BoosterName(ILocalizer localizer, EconomyConfig config, string id)
        {
            var booster = config?.Booster(id);
            return booster == null ? id : localizer.Get(booster.NameKey);
        }

        // share 0..1 as "12.5%" in the language's form
        public static string Percent(ILocalizer localizer, double share) =>
            localizer.Get(EconomyKeys.WheelPercent, Math.Round(share * 100d, 1).ToString("0.#", CultureInfo.InvariantCulture));
    }
}
