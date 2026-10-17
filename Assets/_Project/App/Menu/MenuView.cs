using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Menu
{
    public sealed class MenuView : MonoBehaviour, IMenuView
    {
        // Each further game's button sits this many canvas units above the previous one's top edge
        private const float GameGap = 16f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text version;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button aboutButton;
        [SerializeField] private RectTransform extraSlot;

        private readonly List<Button> gameButtons = new List<Button>();

        public event Action<int> GameClicked;
        public event Action SettingsClicked;
        public event Action AboutClicked;

        public IReadOnlyList<Button> GameButtons => gameButtons;
        public Button SettingsButton => settingsButton;
        public Button AboutButton => aboutButton;

        // A column under the buttons where features add rows (the showcase's best score, premium, scores)
        public Transform ExtraSlot => extraSlot;

        private void Awake()
        {
            settingsButton.onClick.AddListener(OnSettings);
            aboutButton.onClick.AddListener(OnAbout);
        }

        private void OnDestroy()
        {
            settingsButton.onClick.RemoveListener(OnSettings);
            aboutButton.onClick.RemoveListener(OnAbout);
            foreach (var button in gameButtons) button.onClick.RemoveAllListeners();
        }

        public void SetTitle(string text) => title.text = text;

        public void SetVersion(string text) => version.text = text;

        // The scene's Play button serves the first game; copies above it serve the rest (the upstream template has the
        // sample and both showcase games, a generated project one). Copies are bound here: the scene was bound before.
        public void ShowGames(IReadOnlyList<string> labelKeys, UiBinder binder)
        {
            for (var i = 0; i < labelKeys.Count; i++)
            {
                var button = i == 0 ? playButton : Instantiate(playButton, playButton.transform.parent);
                if (i > 0)
                {
                    var rect = (RectTransform)button.transform;
                    var first = (RectTransform)playButton.transform;
                    rect.anchoredPosition = first.anchoredPosition + new Vector2(0f, (first.rect.height + GameGap) * i);
                }
                var index = i;
                button.onClick.AddListener(() => GameClicked?.Invoke(index));
                button.GetComponentInChildren<LocalizedText>(true).SetKey(labelKeys[i]);
                if (i > 0) binder.BindTree(button.gameObject);
                gameButtons.Add(button);
            }
            playButton.gameObject.SetActive(labelKeys.Count > 0);
        }

        public void ShowLevel(int index, int level)
        {
            if (index < 0 || index >= gameButtons.Count) return;
            gameButtons[index].GetComponentInChildren<LocalizedText>(true).SetKey(ShellKeys.Level, level);
        }

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnAbout() => AboutClicked?.Invoke();
    }
}
