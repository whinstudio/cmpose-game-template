using System;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Game
{
    // Pause button at the top right of the safe area and the panel it opens
    public sealed class PauseView : MonoBehaviour, IPauseView
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button menuButton;

        public event Action PauseClicked;
        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action MenuClicked;

        private void Awake()
        {
            KitScreens.Hide(panel);
            pauseButton.onClick.AddListener(OnPause);
            resumeButton.onClick.AddListener(OnResume);
            settingsButton.onClick.AddListener(OnSettings);
            menuButton.onClick.AddListener(OnMenu);
        }

        private void OnDestroy()
        {
            pauseButton.onClick.RemoveListener(OnPause);
            resumeButton.onClick.RemoveListener(OnResume);
            settingsButton.onClick.RemoveListener(OnSettings);
            menuButton.onClick.RemoveListener(OnMenu);
        }

        public void ShowPanel() => KitScreens.Show(panel);

        public void HidePanel() => KitScreens.Hide(panel);

        private void OnPause() => PauseClicked?.Invoke();

        private void OnResume() => ResumeClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnMenu() => MenuClicked?.Invoke();
    }
}
