using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // What the end cards share (SceneBuilder builds them as centred KitSheet modals): the big button (Retry or Next),
    // Menu and, where the card offers it, Continue. The scene's controls under the card that would show through its dim
    // (the pause button) hide while it is open and come back when it closes.
    public abstract class ShellCardView : MonoBehaviour, IShellCard
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private LocalizedText continueLabel;
        [SerializeField] private GameObject[] covers;

        public event Action<EndChoice> Chosen;

        public bool IsShown => panel.activeSelf;

        // The big button's choice
        protected abstract EndChoice Primary { get; }

        protected virtual void Awake()
        {
            KitScreens.Hide(panel);
            primaryButton.onClick.AddListener(OnPrimary);
            menuButton.onClick.AddListener(OnMenu);
            if (continueButton == null) return;
            continueButton.onClick.AddListener(OnContinue);
            continueButton.gameObject.SetActive(false);
        }

        protected virtual void OnDestroy()
        {
            primaryButton.onClick.RemoveListener(OnPrimary);
            menuButton.onClick.RemoveListener(OnMenu);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
        }

        public void ShowContinue(bool shown, bool free)
        {
            if (continueButton == null) return;
            continueButton.gameObject.SetActive(shown);
            if (shown) continueLabel.SetKey(free ? ShellKeys.Continue : ShellKeys.ContinueAd);
        }

        public void Hide()
        {
            KitScreens.Hide(panel);
            Cover(false);
        }

        protected void Open()
        {
            Cover(true);
            KitScreens.Show(panel);
        }

        private void Cover(bool covered)
        {
            if (covers == null) return;
            foreach (var control in covers)
                if (control != null) control.SetActive(!covered);
        }

        private void OnPrimary() => Chosen?.Invoke(Primary);

        private void OnMenu() => Chosen?.Invoke(EndChoice.Menu);

        private void OnContinue() => Chosen?.Invoke(EndChoice.Continue);
    }
}
