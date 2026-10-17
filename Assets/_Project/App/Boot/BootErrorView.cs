using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Boot
{
    public sealed class BootErrorView : MonoBehaviour, IBootErrorView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button retryButton;
        private Action retry;

        private void Awake()
        {
            KitScreens.Hide(panel);
            retryButton.onClick.AddListener(OnRetry);
        }

        private void OnDestroy() => retryButton.onClick.RemoveListener(OnRetry);

        public void Show(string messageKey, string detail, Action onRetry)
        {
            if (detail == null) message.SetKey(messageKey);
            else message.SetKey(messageKey, detail);
            retry = onRetry;
            KitScreens.Show(panel);
        }

        private void OnRetry()
        {
            KitScreens.Hide(panel);
            var pending = retry;
            retry = null;
            pending?.Invoke();
        }
    }
}
