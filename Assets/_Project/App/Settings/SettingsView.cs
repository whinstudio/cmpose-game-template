using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Kturker.UnityBase.App.Settings
{
    // One prefab (SettingsPanel.prefab) in the Menu and Game scenes; the root stays active, the panel toggles. The card:
    // the title, the round Close at its top right corner, Body (the switch rows and the language row in Rows; RemoveAds
    // as the one primary over the grid of the other buttons in Actions; SettingsCardLayout puts them side by side in
    // landscape) and the footer (the legal links, the version; a message shows in their place while it lasts, so the
    // card keeps its height). The language page opens over the card in its place: the card keeps its size, the page's
    // list scrolls; every language row has a ring, the chosen one a filled one with a tick. Hidden buttons and rows
    // leave no gap; the texts of a group share one size (KitTextGroup). Android's back key goes back from the language page, otherwise
    // closes the card. The Credits panel draws over the card.
    public sealed class SettingsView : MonoBehaviour, ISettingsView
    {
        // "Now: {0}" under "Device language", {0} the language the device language gives
        public const string DeviceLanguageNowKey = "settings.deviceLanguageNow";

        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        // What the language page covers: they stay laid out (the card keeps its size) but hide and take no touches
        [SerializeField] private CanvasGroup[] page = Array.Empty<CanvasGroup>();
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private GameObject languageRow;
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageCode;
        [SerializeField] private GameObject actions;
        [SerializeField] private Button primaryButton;
        [SerializeField] private GameObject grid;
        // In SettingsButton order after RemoveAds: Restore, SignIn, AdPrivacy, Support, Rate, Credits
        [SerializeField] private Button[] gridButtons = Array.Empty<Button>();
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button privacyLink;
        [SerializeField] private GameObject linkDot;
        [SerializeField] private Button termsLink;
        [SerializeField] private TMP_Text version;
        [SerializeField] private GameObject languages;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect languageList;
        [SerializeField] private Button deviceButton;
        [SerializeField] private LocalizedText deviceNow;
        // The filled ring with the tick in "Device language"'s mark (each row's Mark/Fill)
        [SerializeField] private GameObject deviceChosen;
        // A language's row, copied for each language (hidden)
        [SerializeField] private Button languageTemplate;
        [SerializeField] private CreditsPanel credits;

        private readonly List<(string Code, Button Button, GameObject Chosen)> rows = new List<(string, Button, GameObject)>();
        private UiBinder binder;
        private bool privacyShown;
        private bool termsShown;
        private bool messageShown;

        public event Action<bool> MusicToggled;
        public event Action<bool> SfxToggled;
        public event Action<bool> VibrationToggled;
        public event Action<SettingsButton> ButtonClicked;
        public event Action LanguageClicked;
        public event Action LanguagesBackClicked;
        public event Action PrivacyClicked;
        public event Action TermsClicked;
        public event Action CloseClicked;
        public event Action CreditsCloseClicked;
        public event Action<string> LanguageChosen;

        public bool IsOpen => panel.activeSelf;
        public bool LanguagesOpen => languages.activeSelf;
        public Toggle SfxSwitch => sfxToggle;
        public Button CloseButton => closeButton;
        public Button CreditsButton => Grid(SettingsButton.Credits);
        public CreditsPanel Credits => credits;

        // Binds the language rows copied at runtime (the scene itself was bound before); without it (EditMode tests)
        // they keep the builder's colours
        [Inject]
        public void Construct(UiBinder uiBinder) => binder = uiBinder;

        private void Awake()
        {
            KitScreens.Hide(panel);
            languages.SetActive(false);
            languageTemplate.gameObject.SetActive(false);
            closeButton.onClick.AddListener(OnClose);
            musicToggle.onValueChanged.AddListener(OnMusic);
            sfxToggle.onValueChanged.AddListener(OnSfx);
            vibrationToggle.onValueChanged.AddListener(OnVibration);
            languageButton.onClick.AddListener(OnLanguage);
            primaryButton.onClick.AddListener(OnPrimary);
            for (var i = 0; i < gridButtons.Length; i++)
            {
                var button = (SettingsButton)(i + 1);
                gridButtons[i].onClick.AddListener(() => ButtonClicked?.Invoke(button));
            }
            privacyLink.onClick.AddListener(OnPrivacy);
            termsLink.onClick.AddListener(OnTerms);
            backButton.onClick.AddListener(OnBack);
            deviceButton.onClick.AddListener(OnDevice);
            credits.CloseClicked += OnCreditsClose;
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnClose);
            musicToggle.onValueChanged.RemoveListener(OnMusic);
            sfxToggle.onValueChanged.RemoveListener(OnSfx);
            vibrationToggle.onValueChanged.RemoveListener(OnVibration);
            languageButton.onClick.RemoveListener(OnLanguage);
            primaryButton.onClick.RemoveListener(OnPrimary);
            foreach (var button in gridButtons) button.onClick.RemoveAllListeners();
            privacyLink.onClick.RemoveListener(OnPrivacy);
            termsLink.onClick.RemoveListener(OnTerms);
            backButton.onClick.RemoveListener(OnBack);
            deviceButton.onClick.RemoveListener(OnDevice);
            credits.CloseClicked -= OnCreditsClose;
        }

        // Android's back key (Escape in the Input System): the credits, then the language page, then the card
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!IsOpen || keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (credits.IsOpen) OnCreditsClose();
            else if (LanguagesOpen) OnBack();
            else OnClose();
        }

        public void Show(bool music, bool sfx, bool vibration)
        {
            Set(musicToggle, music);
            Set(sfxToggle, sfx);
            Set(vibrationToggle, vibration);
            KitScreens.Show(panel);
        }

        public void ShowLanguageRow(string code)
        {
            languageRow.SetActive(code != null);
            if (code != null) languageCode.text = $"{code} ›";
        }

        public void ShowButtons(IReadOnlyList<SettingsButton> buttons)
        {
            var any = false;
            var shown = Contains(buttons, SettingsButton.RemoveAds);
            primaryButton.gameObject.SetActive(shown);
            any |= shown;
            var anyInGrid = false;
            for (var i = 0; i < gridButtons.Length; i++)
            {
                shown = Contains(buttons, (SettingsButton)(i + 1));
                gridButtons[i].gameObject.SetActive(shown);
                anyInGrid |= shown;
            }
            grid.SetActive(anyInGrid);
            actions.SetActive(any || anyInGrid);
        }

        public void ShowLinks(bool privacy, bool terms)
        {
            privacyShown = privacy;
            termsShown = terms;
            ShowFooter();
        }

        public void ShowVersion(string label) => version.text = label;

        // In the footer's place
        public void ShowMessage(string key)
        {
            messageShown = key != null;
            if (key == null) message.SetLiteral(string.Empty);
            else message.SetKey(key);
            ShowFooter();
        }

        public void ShowLanguages(IReadOnlyList<(string Code, string Name)> list, string selected, string currentName)
        {
            Rebuild(list);
            deviceNow.SetKey(DeviceLanguageNowKey, currentName);
            deviceChosen.SetActive(selected == null);
            foreach (var row in rows) row.Chosen.SetActive(row.Code == selected);
            if (LanguagesOpen) return;
            foreach (var covered in page) Veil(covered, true);
            languages.SetActive(true);
            RefreshBand();
            languageList.StopMovement();
            languageList.verticalNormalizedPosition = 1f;
        }

        public void HideLanguages()
        {
            languages.SetActive(false);
            foreach (var covered in page) Veil(covered, false);
            RefreshBand();
        }

        // The card's title band (KitCard) goes under the language page (its veiled title is no header, and Back and the
        // page's title would sit on the band) and comes back with the card's page: the card does not resize for either
        private void RefreshBand()
        {
            var sheet = panel != null ? panel.GetComponent<KitSheet>() : null;
            var card = sheet != null && sheet.Panel != null ? sheet.Panel.GetComponent<KitCard>() : null;
            if (card != null) card.RefreshBand();
        }

        public void ShowCredits(string text) => credits.Show(text);

        public void HideCredits() => credits.Hide();

        public void Hide()
        {
            KitScreens.Hide(panel);
            HideLanguages();
        }

        public Button Grid(SettingsButton button)
        {
            var index = (int)button - 1;
            return index >= 0 && index < gridButtons.Length ? gridButtons[index] : null;
        }

        // One copy of the template per language, in the list's order, made again only when the languages change
        private void Rebuild(IReadOnlyList<(string Code, string Name)> list)
        {
            if (Same(list)) return;
            foreach (var row in rows) Destroy(row.Button.gameObject);
            rows.Clear();
            foreach (var (code, name) in list)
            {
                var button = Instantiate(languageTemplate, languageTemplate.transform.parent, false);
                button.name = code;
                button.gameObject.SetActive(true);
                button.GetComponent<KitButton>().Label.GetComponent<LocalizedText>().SetLiteral(name);
                var chosen = code;
                button.onClick.AddListener(() => OnChosen(chosen));
                binder?.BindTree(button.gameObject);
                rows.Add((code, button, button.transform.Find("Mark/Fill").gameObject));
            }
        }

        private bool Same(IReadOnlyList<(string Code, string Name)> list)
        {
            if (list.Count != rows.Count) return false;
            for (var i = 0; i < list.Count; i++)
                if (list[i].Code != rows[i].Code) return false;
            return true;
        }

        private void ShowFooter()
        {
            privacyLink.gameObject.SetActive(privacyShown && !messageShown);
            termsLink.gameObject.SetActive(termsShown && !messageShown);
            linkDot.SetActive(privacyShown && termsShown && !messageShown);
            version.gameObject.SetActive(!messageShown);
            message.gameObject.SetActive(messageShown);
        }

        // The switch's knob and colour follow at once, also before the switch first updates
        private static void Set(Toggle toggle, bool on)
        {
            var kit = toggle.GetComponent<KitSwitch>();
            if (kit != null) kit.SetWithoutNotify(on);
            else toggle.SetIsOnWithoutNotify(on);
        }

        private static void Veil(CanvasGroup group, bool veiled)
        {
            group.alpha = veiled ? 0f : 1f;
            group.interactable = !veiled;
            group.blocksRaycasts = !veiled;
        }

        private static bool Contains(IReadOnlyList<SettingsButton> buttons, SettingsButton button)
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == button) return true;
            }
            return false;
        }

        private void OnMusic(bool on) => MusicToggled?.Invoke(on);

        private void OnSfx(bool on) => SfxToggled?.Invoke(on);

        private void OnVibration(bool on) => VibrationToggled?.Invoke(on);

        private void OnLanguage() => LanguageClicked?.Invoke();

        private void OnPrimary() => ButtonClicked?.Invoke(SettingsButton.RemoveAds);

        private void OnPrivacy() => PrivacyClicked?.Invoke();

        private void OnTerms() => TermsClicked?.Invoke();

        private void OnBack() => LanguagesBackClicked?.Invoke();

        private void OnDevice() => LanguageChosen?.Invoke(null);

        private void OnChosen(string code) => LanguageChosen?.Invoke(code);

        private void OnCreditsClose() => CreditsCloseClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
