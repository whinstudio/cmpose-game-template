using System;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Game
{
    // "Start here" panel over the Sample feature in the blank shell
    public sealed class GuideView : MonoBehaviour, IGuideView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button gotItButton;

        public event Action DismissClicked;

        private void Awake()
        {
            KitScreens.Hide(panel);
            gotItButton.onClick.AddListener(OnDismiss);
        }

        private void OnDestroy() => gotItButton.onClick.RemoveListener(OnDismiss);

        public void Show() => KitScreens.Show(panel);

        public void Hide() => KitScreens.Hide(panel);

        private void OnDismiss() => DismissClicked?.Invoke();
    }
}
