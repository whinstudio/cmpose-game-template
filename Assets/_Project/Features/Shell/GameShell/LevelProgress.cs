using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Save;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // saveSystem flag: the level flow's progress survives restarts (slot 'levels'): the current level and the best
    // stars of each completed one
    public sealed class LevelProgress : ILevelProgress
    {
        public static readonly SaveSchema<Progress> Schema = new SaveSchema<Progress>("levels", 1);

        private readonly ISaveStore store;
        private Progress progress;
        private bool loaded;

        public LevelProgress(ISaveStore store) => this.store = store;

        public bool Available => true;
        public bool Started => Loaded() != null;
        public int Current => Loaded()?.Current ?? 1;

        public int StarsOf(int level)
        {
            var current = Loaded();
            if (current == null || level < 1 || level > current.Stars.Count) return 0;
            return current.Stars[level - 1];
        }

        public void Start()
        {
            if (Loaded() != null) return;
            progress = new Progress();
            store.Save(Schema, progress);
        }

        public void Complete(int level, int stars)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "levels start at 1");
            Start();
            var current = Loaded();
            var clamped = Mathf.Clamp(stars, 0, GameShell.MaxStars);
            while (current.Stars.Count < level) current.Stars.Add(0);
            current.Stars[level - 1] = Math.Max(current.Stars[level - 1], clamped);
            if (level >= current.Current) current.Current = level == int.MaxValue ? level : level + 1;
            store.Save(Schema, current);
        }

        // Null until the flow starts; a damaged save reads as the first level
        private Progress Loaded()
        {
            if (loaded) return progress;
            loaded = true;
            progress = store.Load(Schema);
            if (progress == null) return null;
            if (progress.Current < 1) progress.Current = 1;
            for (var i = 0; i < progress.Stars.Count; i++) progress.Stars[i] = Mathf.Clamp(progress.Stars[i], 0, GameShell.MaxStars);
            return progress;
        }

        [Serializable]
        public sealed class Progress
        {
            [SerializeField] private int current = 1;
            [SerializeField] private List<int> stars = new List<int>();

            public int Current
            {
                get => current;
                set => current = value;
            }

            public List<int> Stars
            {
                get
                {
                    if (stars == null) stars = new List<int>();
                    return stars;
                }
            }
        }
    }
}
