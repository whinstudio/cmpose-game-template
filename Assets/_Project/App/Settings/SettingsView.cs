using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Settings
{
    // One prefab (SettingsPanel.prefab) in the Menu and Game scenes; the root stays active, the panel toggles
    public sealed class SettingsView : MonoBehaviour, ISettingsView
    {
        public const int RowSlots = 4;

        [SerializeField] private GameObject panel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private SettingsRowView[] rows;
        [SerializeField] private Button rateButton;
        [SerializeField] private Button aboutButton;
        [SerializeField] private Button closeButton;
        private Action[] rowHandlers;

        public event Action<float> MusicChanged;
        public event Action<float> SfxChanged;
        public event Action<bool> VibrationChanged;
        public event Action RateClicked;
        public event Action AboutClicked;
        public event Action CloseClicked;
        public event Action<int> SectionClicked;

        public bool IsOpen => panel.activeSelf;
        public Slider SfxSlider => sfxSlider;
        public Button CloseButton => closeButton;
        public Button AboutButton => aboutButton;

        private void Awake()
        {
            KitScreens.Hide(panel);
            musicSlider.onValueChanged.AddListener(OnMusic);
            sfxSlider.onValueChanged.AddListener(OnSfx);
            vibrationToggle.onValueChanged.AddListener(OnVibration);
            rateButton.onClick.AddListener(OnRate);
            aboutButton.onClick.AddListener(OnAbout);
            closeButton.onClick.AddListener(OnClose);
            rowHandlers = new Action[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var index = i;
                rowHandlers[i] = () => SectionClicked?.Invoke(index);
                rows[i].Clicked += rowHandlers[i];
            }
        }

        private void OnDestroy()
        {
            musicSlider.onValueChanged.RemoveListener(OnMusic);
            sfxSlider.onValueChanged.RemoveListener(OnSfx);
            vibrationToggle.onValueChanged.RemoveListener(OnVibration);
            rateButton.onClick.RemoveListener(OnRate);
            aboutButton.onClick.RemoveListener(OnAbout);
            closeButton.onClick.RemoveListener(OnClose);
            for (var i = 0; i < rows.Length; i++) rows[i].Clicked -= rowHandlers[i];
        }

        public void Show(float music, float sfx, bool vibration)
        {
            musicSlider.SetValueWithoutNotify(music);
            sfxSlider.SetValueWithoutNotify(sfx);
            vibrationToggle.SetIsOnWithoutNotify(vibration);
            KitScreens.Show(panel);
        }

        public void SetRateVisible(bool visible) => rateButton.gameObject.SetActive(visible);

        public void SetAboutVisible(bool visible) => aboutButton.gameObject.SetActive(visible);

        // Rows beyond the slots are not shown (RowSlots: SceneBuilder builds that many)
        public void ShowSections(IReadOnlyList<SettingsRow> sections)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                if (i < sections.Count) rows[i].Show(sections[i]);
                else rows[i].Hide();
            }
        }

        public void Hide() => KitScreens.Hide(panel);

        private void OnMusic(float value) => MusicChanged?.Invoke(value);

        private void OnSfx(float value) => SfxChanged?.Invoke(value);

        private void OnVibration(bool value) => VibrationChanged?.Invoke(value);

        private void OnRate() => RateClicked?.Invoke();

        private void OnAbout() => AboutClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
