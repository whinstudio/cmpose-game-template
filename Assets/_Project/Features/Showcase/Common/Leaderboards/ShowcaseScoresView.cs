using System;
using Kturker.UnityBase.Core.Leaderboards;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Showcase.Common.Leaderboards
{
    // Leaderboard and Achievements as round buttons of the menu's right column, and a hint line in the menu's extra slot
    // that shows only with a hint (SceneBuilder builds the line)
    public sealed class ShowcaseScoresView : MonoBehaviour, IShowcaseScoresView
    {
        public const string ResourcePath = "ShowcaseLeaderboards/Scores";

        [SerializeField] private LocalizedText hint;

        public event Action LeaderboardClicked;
        public event Action AchievementsClicked;

        public static ShowcaseScoresView Spawn(Transform slot, IMenuSideButtons sides, UiBinder binder)
        {
            var prefab = Resources.Load<ShowcaseScoresView>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException($"missing Resources/{ResourcePath}");
            var view = Instantiate(prefab, slot, false);
            binder.BindTree(view.gameObject);
            sides.AddSideButton(MenuSide.Right, KitSprites.Podium, LeaderboardsKeys.Leaderboard, view.OnLeaderboard);
            sides.AddSideButton(MenuSide.Right, KitSprites.Star, LeaderboardsKeys.Achievements, view.OnAchievements);
            view.HideHint();
            return view;
        }

        public void ShowHint(string key)
        {
            hint.SetKey(key);
            gameObject.SetActive(true);
        }

        // The row leaves the column, so it keeps no gap
        public void HideHint() => gameObject.SetActive(false);

        private void OnLeaderboard() => LeaderboardClicked?.Invoke();

        private void OnAchievements() => AchievementsClicked?.Invoke();
    }
}
