using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Update
{
    // Full-screen opaque panel on its own canvas, above the menu, About and the settings panel
    public sealed class UpdateView : MonoBehaviour, IUpdateView
    {
        public const string RequiredTitleKey = "update.required.title";
        public const string RequiredMessageKey = "update.required.message";
        public const string RequiredStoreMessageKey = "update.required.store";
        public const string AvailableTitleKey = "update.available.title";
        public const string AvailableMessageKey = "update.available.message";

        [SerializeField] private GameObject panel;
        [SerializeField] private LocalizedText title;
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button laterButton;

        public event Action UpdateClicked;
        public event Action LaterClicked;

        public bool IsOpen => panel.activeSelf;

        private void Awake()
        {
            KitScreens.Hide(panel);
            updateButton.onClick.AddListener(OnUpdate);
            laterButton.onClick.AddListener(OnLater);
        }

        private void OnDestroy()
        {
            updateButton.onClick.RemoveListener(OnUpdate);
            laterButton.onClick.RemoveListener(OnLater);
        }

        public void ShowRequired(bool canUpdate)
        {
            title.SetKey(RequiredTitleKey);
            message.SetKey(canUpdate ? RequiredMessageKey : RequiredStoreMessageKey);
            updateButton.gameObject.SetActive(canUpdate);
            laterButton.gameObject.SetActive(false);
            KitScreens.Show(panel);
        }

        public void ShowAvailable()
        {
            title.SetKey(AvailableTitleKey);
            message.SetKey(AvailableMessageKey);
            updateButton.gameObject.SetActive(true);
            laterButton.gameObject.SetActive(true);
            KitScreens.Show(panel);
        }

        public void Hide() => KitScreens.Hide(panel);

        private void OnUpdate() => UpdateClicked?.Invoke();

        private void OnLater() => LaterClicked?.Invoke();
    }
}
