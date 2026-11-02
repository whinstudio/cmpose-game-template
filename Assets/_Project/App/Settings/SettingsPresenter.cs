using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;

namespace Kturker.UnityBase.App.Settings
{
    // The settings card. The switches write through at once; Close saves the preferences once. Opening an open card or
    // closing a closed one does nothing. The buttons keep the SettingsButton order and only the visible ones show: the
    // flags' actions (ISettingsAction), Support (AppConfig.SupportUrl), Rate (a store page) and Credits (the game's
    // credits, then the third-party notices). An action runs once at a time; its message shows for MessageSeconds, and
    // a card closed meanwhile gets none. The language row shows with a language choice of two or more languages; its
    // list opens inside the card. Privacy and Terms show when their URL is set.
    public sealed class SettingsPresenter : Presenter<ISettingsView>, ISettingsPanel
    {
        public const int MessageSeconds = 3;

        private static readonly SettingsButton[] Order = (SettingsButton[])Enum.GetValues(typeof(SettingsButton));

        private readonly AudioPreferences audio;
        private readonly HapticsService haptics;
        private readonly IPreferences preferences;
        private readonly IUiFeedback feedback;
        private readonly ReviewPrompter review;
        private readonly IReadOnlyList<ISettingsAction> actions;
        private readonly ISettingsLanguage language;
        private readonly IUrlOpener urls;
        private readonly string creditsText;
        private readonly string supportUrl;
        private readonly string privacyUrl;
        private readonly string termsUrl;
        private readonly string versionLabel;
        private readonly HashSet<SettingsButton> running = new HashSet<SettingsButton>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        // Counts the openings, so an action that ends after its card closed (and maybe opened again) shows nothing
        private int session;
        // Counts the messages, so a message's timer clears only that message
        private int message;
        private bool languagesOpen;
        private bool creditsOpen;

        public SettingsPresenter(ISettingsView view, AudioPreferences audio, HapticsService haptics, IPreferences preferences,
            IUiFeedback feedback, ReviewPrompter review, IReadOnlyList<ISettingsAction> actions,
            IReadOnlyList<ISettingsLanguage> languages, GameCredits credits, AppConfig config, AppInfo app, IUrlOpener urls)
            : base(view)
        {
            this.audio = audio;
            this.haptics = haptics;
            this.preferences = preferences;
            this.feedback = feedback;
            this.review = review;
            this.actions = actions;
            language = languages.Count > 0 ? languages[0] : null;
            this.urls = urls;
            creditsText = CreditsText(credits, config);
            supportUrl = Openable(config != null ? config.SupportUrl : null);
            privacyUrl = Openable(config != null ? config.PrivacyUrl : null);
            termsUrl = Openable(config != null ? config.TermsUrl : null);
            versionLabel = $"v{app.Version}";
        }

        public bool IsOpen { get; private set; }

        private bool HasLanguageRow => language != null && language.Languages.Count >= 2;

        public override void Start()
        {
            View.MusicToggled += OnMusic;
            View.SfxToggled += OnSfx;
            View.VibrationToggled += OnVibration;
            View.ButtonClicked += OnButton;
            View.LanguageClicked += OnLanguage;
            View.LanguagesBackClicked += OnLanguagesBack;
            View.LanguageChosen += OnLanguageChosen;
            View.PrivacyClicked += OnPrivacy;
            View.TermsClicked += OnTerms;
            View.CloseClicked += OnClose;
            View.CreditsCloseClicked += OnCreditsClose;
            foreach (var action in actions) action.Changed += RenderButtons;
            if (language != null) language.Changed += OnLanguageChanged;
            RenderButtons();
            RenderLanguageRow();
            View.ShowLinks(privacyUrl != null, termsUrl != null);
            View.ShowVersion(versionLabel);
        }

        public override void Dispose()
        {
            View.MusicToggled -= OnMusic;
            View.SfxToggled -= OnSfx;
            View.VibrationToggled -= OnVibration;
            View.ButtonClicked -= OnButton;
            View.LanguageClicked -= OnLanguage;
            View.LanguagesBackClicked -= OnLanguagesBack;
            View.LanguageChosen -= OnLanguageChosen;
            View.PrivacyClicked -= OnPrivacy;
            View.TermsClicked -= OnTerms;
            View.CloseClicked -= OnClose;
            View.CreditsCloseClicked -= OnCreditsClose;
            foreach (var action in actions) action.Changed -= RenderButtons;
            if (language != null) language.Changed -= OnLanguageChanged;
            lifetime.Cancel();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            session++;
            View.Show(audio.MusicOn, audio.SfxOn, haptics.Enabled);
        }

        private void OnMusic(bool on) => audio.MusicOn = on;

        private void OnSfx(bool on) => audio.SfxOn = on;

        private void OnVibration(bool on) => haptics.Enabled = on;

        private void OnButton(SettingsButton button)
        {
            if (!IsOpen || !Visible(button)) return;
            switch (button)
            {
                case SettingsButton.Support:
                    feedback.Click();
                    urls.Open(supportUrl);
                    break;
                case SettingsButton.Rate:
                    feedback.Click();
                    review.OpenStorePage();
                    break;
                case SettingsButton.Credits:
                    OnCredits();
                    break;
                default:
                    var action = VisibleAction(button);
                    if (action == null || !running.Add(button)) return;
                    feedback.Click();
                    RunAsync(action, session).Forget();
                    break;
            }
        }

        private async UniTaskVoid RunAsync(ISettingsAction action, int opened)
        {
            string key;
            try
            {
                key = await action.PressAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                running.Remove(action.Button);
            }
            if (key == null || lifetime.IsCancellationRequested || !IsOpen || session != opened) return;
            ShowMessage(key);
        }

        private void ShowMessage(string key)
        {
            var shown = ++message;
            View.ShowMessage(key);
            ClearLaterAsync(shown).Forget();
        }

        // Unscaled: the Game's pause panel opens this card at time scale 0
        private async UniTaskVoid ClearLaterAsync(int shown)
        {
            var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(MessageSeconds), ignoreTimeScale: true,
                cancellationToken: lifetime.Token).SuppressCancellationThrow();
            if (cancelled || shown != message) return;
            ClearMessage();
        }

        private void ClearMessage()
        {
            message++;
            View.ShowMessage(null);
        }

        private void OnLanguage()
        {
            if (!IsOpen || !HasLanguageRow || languagesOpen) return;
            feedback.Click();
            languagesOpen = true;
            RenderLanguages();
        }

        private void OnLanguagesBack()
        {
            if (!languagesOpen) return;
            feedback.Click();
            HideLanguages();
        }

        // Applies at once; the list stays open
        private void OnLanguageChosen(string code)
        {
            if (!languagesOpen) return;
            feedback.Click();
            if (code == null) language.FollowDevice();
            else language.Choose(code);
        }

        private void OnLanguageChanged()
        {
            RenderLanguageRow();
            if (languagesOpen) RenderLanguages();
        }

        private void HideLanguages()
        {
            languagesOpen = false;
            View.HideLanguages();
        }

        private void OnPrivacy() => OpenLink(privacyUrl);

        private void OnTerms() => OpenLink(termsUrl);

        private void OpenLink(string url)
        {
            if (!IsOpen || url == null) return;
            feedback.Click();
            urls.Open(url);
        }

        // The credits draw over this card, which stays open under them
        private void OnCredits()
        {
            if (creditsOpen) return;
            feedback.Click();
            creditsOpen = true;
            View.ShowCredits(creditsText);
        }

        private void OnCreditsClose()
        {
            if (!creditsOpen) return;
            feedback.Click();
            HideCredits();
        }

        private void HideCredits()
        {
            creditsOpen = false;
            View.HideCredits();
        }

        private void OnClose()
        {
            if (!IsOpen) return;
            feedback.Click();
            if (creditsOpen) HideCredits();
            if (languagesOpen) HideLanguages();
            ClearMessage();
            IsOpen = false;
            preferences.Save();
            View.Hide();
        }

        private void RenderButtons()
        {
            var shown = new List<SettingsButton>(Order.Length);
            foreach (var button in Order)
            {
                if (Visible(button)) shown.Add(button);
            }
            View.ShowButtons(shown);
        }

        private void RenderLanguageRow() =>
            View.ShowLanguageRow(HasLanguageRow ? (language.Current ?? string.Empty).ToUpperInvariant() : null);

        private void RenderLanguages() =>
            View.ShowLanguages(language.Languages, language.FollowsDevice ? null : language.Current, NameOf(language.Device));

        private bool Visible(SettingsButton button)
        {
            switch (button)
            {
                case SettingsButton.Support: return supportUrl != null;
                case SettingsButton.Rate: return review.CanOpenStorePage;
                case SettingsButton.Credits: return creditsText.Length > 0;
                default: return VisibleAction(button) != null;
            }
        }

        private ISettingsAction VisibleAction(SettingsButton button)
        {
            foreach (var action in actions)
            {
                if (action.Button == button && action.Visible) return action;
            }
            return null;
        }

        private string NameOf(string code)
        {
            foreach (var entry in language.Languages)
            {
                if (entry.Code == code) return entry.Name;
            }
            return code;
        }

        // The game's credits, then the third-party notices after a blank line; empty when there are neither
        private static string CreditsText(GameCredits credits, AppConfig config)
        {
            var own = credits != null ? credits.Text : string.Empty;
            var notices = config != null && config.ThirdPartyNotices != null ? config.ThirdPartyNotices.text.Trim() : string.Empty;
            if (own.Length == 0) return notices;
            return notices.Length == 0 ? own : $"{own}\n\n{notices}";
        }

        // A configured link opens only as https, http or mailto; anything else (blank included) hides its button
        private static string Openable(string url)
        {
            var trimmed = (url ?? string.Empty).Trim();
            foreach (var scheme in new[] { "https://", "http://", "mailto:" })
            {
                if (trimmed.Length > scheme.Length && trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return trimmed;
            }
            return null;
        }
    }
}
