using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // "Level n" complete: the stars land one by one, then the reward counts up (and the confetti bursts with the last
    // star); Next is the breathing Play button
    public sealed class LevelCompleteCard : ShellCardView, ILevelCompleteCard
    {
        [SerializeField] private LocalizedText title;
        [SerializeField] private StarRow stars;
        [SerializeField] private CountUpText reward;
        [SerializeField] private Celebration celebration;
        private int pendingReward;
        private bool celebrate;

        public event Action StarFilled;

        protected override EndChoice Primary => EndChoice.Next;

        protected override void Awake()
        {
            base.Awake();
            stars.Filled += OnStarFilled;
            stars.Finished += OnStarsFinished;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            stars.Filled -= OnStarFilled;
            stars.Finished -= OnStarsFinished;
        }

        public void Show(int level, int starCount, int rewardValue)
        {
            title.SetKey(ShellKeys.Level, level);
            celebrate = false;
            pendingReward = rewardValue;
            Open();
            reward.SetValue(0);
            stars.Play(starCount);
        }

        public void Celebrate()
        {
            if (stars.Playing) celebrate = true;
            else celebration.Play();
        }

        private void OnStarFilled() => StarFilled?.Invoke();

        private void OnStarsFinished()
        {
            reward.CountTo(pendingReward);
            if (!celebrate) return;
            celebrate = false;
            celebration.Play();
        }
    }
}
