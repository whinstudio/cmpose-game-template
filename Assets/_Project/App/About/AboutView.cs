using System;
using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.About
{
    public sealed class AboutView : MonoBehaviour, IAboutView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text version;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button termsButton;
        [SerializeField] private TMP_Text licenses;
        [SerializeField] private Button closeButton;

        public event Action PrivacyClicked;
        public event Action TermsClicked;
        public event Action CloseClicked;

        private void Awake()
        {
            KitScreens.Hide(panel);
            privacyButton.onClick.AddListener(OnPrivacy);
            termsButton.onClick.AddListener(OnTerms);
            closeButton.onClick.AddListener(OnClose);
        }

        private void OnDestroy()
        {
            privacyButton.onClick.RemoveListener(OnPrivacy);
            termsButton.onClick.RemoveListener(OnTerms);
            closeButton.onClick.RemoveListener(OnClose);
        }

        public void Show(string versionText, bool privacyVisible, bool termsVisible, string licenseText)
        {
            version.text = versionText;
            privacyButton.gameObject.SetActive(privacyVisible);
            termsButton.gameObject.SetActive(termsVisible);
            licenses.text = licenseText;
            KitScreens.Show(panel);
        }

        public void Hide() => KitScreens.Hide(panel);

        private void OnPrivacy() => PrivacyClicked?.Invoke();

        private void OnTerms() => TermsClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
