using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Menu
{
    // One round button of the menu's side columns with its caption under it and its badges over the circle: a red dot
    // at the top right, a countdown pill across the bottom, a video mark at the top right (SceneBuilder builds the
    // hidden template, MenuView.AddSideButton copies it)
    public sealed class MenuSideButton : MonoBehaviour, IMenuSideButton
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private LocalizedText caption;
        [SerializeField] private GameObject dot;
        [SerializeField] private GameObject timer;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private GameObject ad;
        private Action clicked;

        public MenuBadge Badge { get; private set; }
        public Button Button => button;
        public Image Icon => icon;
        public LocalizedText Caption => caption;
        public GameObject Dot => dot;
        public GameObject Timer => timer;
        public TMP_Text TimerText => timerText;
        public GameObject Ad => ad;

        internal void Setup(Sprite sprite, string labelKey, Action onClick)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            caption.SetKey(labelKey);
            clicked = onClick;
            button.onClick.AddListener(OnClick);
            SetBadge(MenuBadge.None);
        }

        private void OnDestroy() => button.onClick.RemoveListener(OnClick);

        public void SetBadge(MenuBadge badge, string timerValue = null)
        {
            Badge = badge;
            dot.SetActive(badge == MenuBadge.Dot);
            ad.SetActive(badge == MenuBadge.Ad);
            timer.SetActive(badge == MenuBadge.Timer);
            timerText.text = badge == MenuBadge.Timer ? timerValue ?? string.Empty : string.Empty;
        }

        private void OnClick() => clicked?.Invoke();
    }
}
