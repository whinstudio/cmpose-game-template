using System;
using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Settings
{
    // The settings panel's Credits screen (in SettingsPanel.prefab, over the card): the game's credits (GameCredits) in a
    // scrolling box in the body font, shown as written (no rich text); Close goes back to Settings. Compass checks for
    // this file before it writes Resources/Credits.txt: keep its path.
    public sealed class CreditsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button closeButton;

        public event Action CloseClicked;

        public bool IsOpen => panel.activeSelf;
        public TMP_Text Body => body;
        public ScrollRect Scroll => scroll;
        public Button CloseButton => closeButton;

        private void Awake()
        {
            KitScreens.Hide(panel);
            closeButton.onClick.AddListener(OnClose);
        }

        private void OnDestroy() => closeButton.onClick.RemoveListener(OnClose);

        // Opens at the top of the text
        public void Show(string text)
        {
            body.text = text;
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
            KitScreens.Show(panel);
        }

        public void Hide() => KitScreens.Hide(panel);

        private void OnClose() => CloseClicked?.Invoke();
    }
}
