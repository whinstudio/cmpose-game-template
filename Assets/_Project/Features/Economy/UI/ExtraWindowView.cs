using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows: a centred modal with the title, the empty Content area a custom
    // extra fills and Close
    public sealed class ExtraWindowView : MonoBehaviour, IExtraWindowView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private LocalizedText title;
        [SerializeField] private RectTransform content;
        [SerializeField] private Button closeButton;

        public event Action CloseClicked;

        public RectTransform Content => content;

        private void Awake()
        {
            KitScreens.Hide(panel);
            closeButton.onClick.AddListener(OnClose);
        }

        private void OnDestroy() => closeButton.onClick.RemoveListener(OnClose);

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowTitle(string key) => title.SetKey(key);

        public void SetBusy(bool busy) => closeButton.interactable = !busy;

        private void OnClose() => CloseClicked?.Invoke();
    }
}
