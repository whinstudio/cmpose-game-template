using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Showcase.Common
{
    // The game-over panel of a showcase game scene (SceneBuilder builds it with the game shell's game-over card layout:
    // the score counting up, "New best!" with confetti); flag parts put their rows into ActionSlot
    public sealed class GameOverView : MonoBehaviour, IGameOverView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CountUpText score;
        [SerializeField] private LocalizedText best;
        [SerializeField] private KitBadge newBest;
        [SerializeField] private Celebration celebration;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private RectTransform actionSlot;

        public event Action RetryClicked;
        public event Action MenuClicked;

        public Transform ActionSlot => actionSlot;
        public bool IsShown => panel.activeSelf;

        private void Awake()
        {
            KitScreens.Hide(panel);
            retryButton.onClick.AddListener(OnRetry);
            menuButton.onClick.AddListener(OnMenu);
        }

        private void OnDestroy()
        {
            retryButton.onClick.RemoveListener(OnRetry);
            menuButton.onClick.RemoveListener(OnMenu);
        }

        public void Show(int scoreValue, int bestValue, bool isNewBest)
        {
            best.SetKey(ShowcaseKeys.Best, bestValue);
            newBest.gameObject.SetActive(isNewBest);
            KitScreens.Show(panel);
            score.SetValue(0);
            score.CountTo(scoreValue);
            if (!isNewBest) return;
            newBest.Pop();
            celebration.Play();
        }

        public void Hide() => KitScreens.Hide(panel);

        private void OnRetry() => RetryClicked?.Invoke();

        private void OnMenu() => MenuClicked?.Invoke();
    }
}
