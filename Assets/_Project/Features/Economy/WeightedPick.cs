using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.Features.Economy
{
    // A random reward by weight (a wheel slice, a chest's prize): each item is picked with its weight's share of the
    // total. Odds gives those shares, which a window that hands out random rewards must show. Weights below 1 are
    // never picked; with none at 1 or above nothing can be picked.
    public static class WeightedPick
    {
        // The picked index, -1 when no weight is at least 1
        public static int Index(IReadOnlyList<int> weights, Random random)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (random == null) throw new ArgumentNullException(nameof(random));
            long total = 0;
            foreach (var weight in weights) total += Math.Max(0, weight);
            if (total == 0) return -1;
            var roll = total <= int.MaxValue ? random.Next((int)total) : Math.Min(total - 1, (long)(random.NextDouble() * total));
            for (var i = 0; i < weights.Count; i++)
            {
                var weight = Math.Max(0, weights[i]);
                if (roll < weight) return i;
                roll -= weight;
            }
            return weights.Count - 1;
        }

        // The picked item (default when none can be picked)
        public static T Pick<T>(IReadOnlyList<T> items, Func<T, int> weight, Random random)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var index = Index(Weights(items, weight), random);
            return index < 0 ? default : items[index];
        }

        // Each weight's share of the total, 0..1, in order (all 0 when the total is 0)
        public static IReadOnlyList<double> Odds(IReadOnlyList<int> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            long total = 0;
            foreach (var weight in weights) total += Math.Max(0, weight);
            var odds = new double[weights.Count];
            for (var i = 0; i < odds.Length; i++) odds[i] = total > 0 ? Math.Max(0, weights[i]) / (double)total : 0d;
            return odds;
        }

        public static IReadOnlyList<double> Odds<T>(IReadOnlyList<T> items, Func<T, int> weight) => Odds(Weights(items, weight));

        private static int[] Weights<T>(IReadOnlyList<T> items, Func<T, int> weight)
        {
            if (weight == null) throw new ArgumentNullException(nameof(weight));
            var weights = new int[items.Count];
            for (var i = 0; i < weights.Length; i++) weights[i] = weight(items[i]);
            return weights;
        }
    }
}
