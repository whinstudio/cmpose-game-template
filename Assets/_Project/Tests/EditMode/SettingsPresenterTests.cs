using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class SettingsPresenterTests
    {
        private static readonly (string Code, string Name)[] Two = { ("en", "English"), ("tr", "Türkçe") };

        private readonly List<Object> made = new List<Object>();
        private FakePreferences prefs;
        private FakeSettingsView view;
        private FakeUiFeedback feedback;
        private FakeUrlOpener urls;
        private AudioPreferences audio;
        private HapticsService haptics;
        private FakeReviewRequester requester;

        [SetUp]
        public void Create()
        {
            prefs = new FakePreferences();
            view = new FakeSettingsView();
            feedback = new FakeUiFeedback();
            urls = new FakeUrlOpener();
            audio = new AudioPreferences(prefs);
            haptics = new HapticsService(prefs, new FakeVibrator());
            requester = new FakeReviewRequester();
        }

        [TearDown]
        public void Destroy()
        {
            foreach (var item in made) Object.DestroyImmediate(item);
            made.Clear();
        }

        private ReviewPrompter Review(StorePlatform platform = StorePlatform.Android) =>
            new ReviewPrompter(prefs, requester, new StoreIdentity(platform, "com.example.game", ""), new FakeLog());

        private AppConfig Config(string privacy = "", string terms = "", string support = "", string notices = null)
        {
            var config = ScriptableObject.CreateInstance<AppConfig>();
            made.Add(config);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("privacyUrl").stringValue = privacy;
            serialized.FindProperty("termsUrl").stringValue = terms;
            serialized.FindProperty("supportUrl").stringValue = support;
            if (notices != null)
            {
                var file = new TextAsset(notices);
                made.Add(file);
                serialized.FindProperty("thirdPartyNotices").objectReferenceValue = file;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private SettingsPresenter Start(FakeSettingsView settingsView = null, IReadOnlyList<ISettingsAction> actions = null,
            IReadOnlyList<ISettingsLanguage> languages = null, GameCredits credits = null, AppConfig config = null,
            ReviewPrompter review = null, string version = "1.0.0")
        {
            var presenter = new SettingsPresenter(settingsView ?? view, audio, haptics, prefs, feedback, review ?? Review(),
                actions ?? new List<ISettingsAction>(), languages ?? new List<ISettingsLanguage>(), credits ?? GameCredits.None,
                config ?? Config(), new AppInfo("UnityBase", version, "Android"), urls);
            presenter.Start();
            return presenter;
        }

        [Test]
        public void Switches_write_through()
        {
            audio.SfxOn = false;
            var presenter = Start();
            presenter.Open();

            Assert.That(view.Shown, Is.EqualTo(1));
            Assert.That(view.Music, Is.True);
            Assert.That(view.Sfx, Is.False);
            Assert.That(view.Vibration, Is.True);

            view.ToggleMusic(false);
            view.ToggleSfx(true);
            view.ToggleVibration(false);

            Assert.That(audio.MusicOn, Is.False);
            Assert.That(audio.SfxOn, Is.True);
            Assert.That(haptics.Enabled, Is.False);
            Assert.That(prefs.Saves, Is.EqualTo(0), "Close saves");

            presenter.Dispose();
            view.ToggleMusic(true);
            Assert.That(audio.MusicOn, Is.False, "a disposed presenter ignores the switches");
        }

        [Test]
        public void Buttons_follow_the_fixed_order_and_only_the_visible_ones_show()
        {
            var actions = new List<ISettingsAction>
            {
                new FakeSettingsAction(SettingsButton.AdPrivacy),
                new FakeSettingsAction(SettingsButton.SignIn) { Visible = false },
                new FakeSettingsAction(SettingsButton.Restore),
                new FakeSettingsAction(SettingsButton.RemoveAds)
            };
            Start(actions: actions, credits: new GameCredits("Art by Ada"), config: Config(support: "mailto:help@example.com"));

            Assert.That(view.Buttons, Is.EqualTo(new[]
            {
                SettingsButton.RemoveAds, SettingsButton.Restore, SettingsButton.AdPrivacy, SettingsButton.Support,
                SettingsButton.Rate, SettingsButton.Credits
            }));

            var bare = new FakeSettingsView();
            Start(bare, review: Review(StorePlatform.Ios));
            Assert.That(bare.Buttons, Is.Empty, "no actions, no support, no store page on iOS without an ID, no credits");
        }

        [Test]
        public void A_changed_action_redraws_the_buttons()
        {
            var removeAds = new FakeSettingsAction(SettingsButton.RemoveAds);
            var restore = new FakeSettingsAction(SettingsButton.Restore);
            var presenter = Start(actions: new List<ISettingsAction> { removeAds, restore });
            presenter.Open();
            Assert.That(view.Buttons, Is.EqualTo(new[] { SettingsButton.RemoveAds, SettingsButton.Restore, SettingsButton.Rate }));

            // Remove Ads bought while the card is open: the primary goes, the grid closes up
            removeAds.Visible = false;
            Assert.That(view.Buttons, Is.EqualTo(new[] { SettingsButton.Restore, SettingsButton.Rate }));

            view.Click(SettingsButton.RemoveAds);
            Assert.That(removeAds.Presses, Is.Zero, "a hidden button does nothing");

            removeAds.Visible = true;
            Assert.That(view.Buttons, Is.EqualTo(new[] { SettingsButton.RemoveAds, SettingsButton.Restore, SettingsButton.Rate }));

            var renders = view.ButtonRenders;
            presenter.Dispose();
            removeAds.Visible = false;
            Assert.That(view.ButtonRenders, Is.EqualTo(renders), "a disposed presenter stops listening");
        }

        [Test]
        public void Restore_pressed_twice_runs_once_and_a_closed_card_gets_no_message()
        {
            var restore = new FakeSettingsAction(SettingsButton.Restore) { Pending = new UniTaskCompletionSource<string>() };
            var presenter = Start(actions: new List<ISettingsAction> { restore });

            view.Click(SettingsButton.Restore);
            Assert.That(restore.Presses, Is.Zero, "a closed card ignores its buttons");

            presenter.Open();
            view.Click(SettingsButton.Restore);
            view.Click(SettingsButton.Restore);
            Assert.That(restore.Presses, Is.EqualTo(1), "the second press waits for the first");
            Assert.That(feedback.Clicks, Is.EqualTo(1));

            view.ClickClose();
            restore.Pending.TrySetResult("purchases.restored");
            Assert.That(view.Message, Is.Null, "closed before the restore ended");
            Assert.That(view.Messages, Has.None.EqualTo("purchases.restored"));

            // Done: the next press runs and its answer shows on the open card
            restore.Pending = null;
            restore.Answer = "purchases.error.network";
            presenter.Open();
            view.Click(SettingsButton.Restore);
            Assert.That(restore.Presses, Is.EqualTo(2));
            Assert.That(view.Message, Is.EqualTo("purchases.error.network"));

            // Closing clears the message at once
            view.ClickClose();
            Assert.That(view.Message, Is.Null);
        }

        [Test]
        public void An_action_that_ends_after_the_card_reopened_shows_nothing()
        {
            var restore = new FakeSettingsAction(SettingsButton.Restore) { Pending = new UniTaskCompletionSource<string>() };
            var presenter = Start(actions: new List<ISettingsAction> { restore });
            presenter.Open();
            view.Click(SettingsButton.Restore);
            view.ClickClose();
            presenter.Open();

            restore.Pending.TrySetResult("purchases.restored");

            Assert.That(view.Message, Is.Null);
        }

        [Test]
        public void Support_shows_only_with_a_support_url()
        {
            Start(config: Config(support: "   "));
            Assert.That(view.Buttons, Has.No.Member(SettingsButton.Support), "blank");

            var unsafeView = new FakeSettingsView();
            Start(unsafeView, config: Config(support: "javascript:alert(1)"));
            Assert.That(unsafeView.Buttons, Has.No.Member(SettingsButton.Support), "not https, http or mailto");

            var withSupport = new FakeSettingsView();
            var presenter = Start(withSupport, config: Config(support: " https://whin.studio/apps/reef-submarine/ "));
            Assert.That(withSupport.Buttons, Has.Member(SettingsButton.Support));

            withSupport.Click(SettingsButton.Support);
            Assert.That(urls.Opened, Is.Empty, "a closed card ignores its buttons");

            presenter.Open();
            withSupport.Click(SettingsButton.Support);
            Assert.That(urls.Opened, Is.EqualTo(new[] { "https://whin.studio/apps/reef-submarine/" }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Rate_opens_the_play_store_page_and_hides_on_ios_without_an_app_store_id()
        {
            var presenter = Start();
            presenter.Open();
            Assert.That(view.Buttons, Has.Member(SettingsButton.Rate));

            view.Click(SettingsButton.Rate);

            Assert.That(requester.StorePages, Is.EqualTo(new[]
            {
                ("market://details?id=com.example.game", "https://play.google.com/store/apps/details?id=com.example.game")
            }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));

            var ios = new FakeSettingsView();
            Start(ios, review: Review(StorePlatform.Ios));
            Assert.That(ios.Buttons, Has.No.Member(SettingsButton.Rate));
        }

        [Test]
        public void Links_hide_when_their_url_is_blank()
        {
            var presenter = Start(config: Config(privacy: " ", terms: "https://whin.studio/legal/reef-submarine/terms/"));
            Assert.That(view.PrivacyLink, Is.False);
            Assert.That(view.TermsLink, Is.True);

            presenter.Open();
            view.ClickPrivacy();
            view.ClickTerms();
            Assert.That(urls.Opened, Is.EqualTo(new[] { "https://whin.studio/legal/reef-submarine/terms/" }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));

            var both = new FakeSettingsView();
            Start(both, config: Config(privacy: "https://whin.studio/legal/reef-submarine/privacy/", terms: "https://example.com/t"));
            Assert.That(both.PrivacyLink, Is.True);
            Assert.That(both.TermsLink, Is.True);
        }

        [Test]
        public void The_version_label_is_v_and_the_app_version()
        {
            Start(version: "2.3.1");

            Assert.That(view.Version, Is.EqualTo("v2.3.1"));
        }

        [Test]
        public void The_language_row_shows_only_with_two_or_more_languages()
        {
            Start();
            Assert.That(view.LanguageRow, Is.Null, "no language choice");

            var one = new FakeSettingsView();
            Start(one, languages: new List<ISettingsLanguage> { new FakeSettingsLanguage(("en", "English")) });
            Assert.That(one.LanguageRow, Is.Null, "one language");

            var two = new FakeSettingsView();
            var language = new FakeSettingsLanguage(Two) { DeviceLanguage = "tr" };
            var presenter = Start(two, languages: new List<ISettingsLanguage> { language });
            Assert.That(two.LanguageRow, Is.EqualTo("TR"), "the short code");

            two.ClickLanguage();
            Assert.That(two.LanguagesOpen, Is.False, "a closed card ignores the row");

            presenter.Open();
            two.ClickLanguage();
            Assert.That(two.LanguagesOpen, Is.True);
            Assert.That(two.Languages, Is.EqualTo(Two));
            Assert.That(two.LanguageSelected, Is.Null, "the device language is followed");
            Assert.That(two.LanguageCurrentName, Is.EqualTo("Türkçe"));

            two.ChooseLanguage("en");
            Assert.That(language.Calls, Is.EqualTo(new[] { "choose en" }));
            Assert.That(two.LanguageRow, Is.EqualTo("EN"));
            Assert.That(two.LanguagesOpen, Is.True, "the list stays open");
            Assert.That(two.LanguageSelected, Is.EqualTo("en"));
            Assert.That(two.LanguageCurrentName, Is.EqualTo("Türkçe"), "under Device language: the device's, not the chosen one");

            two.ClickLanguagesBack();
            Assert.That(two.LanguagesOpen, Is.False);
            Assert.That(presenter.IsOpen, Is.True, "back returns to the card");
        }

        [Test]
        public void Choosing_device_language_follows_the_device()
        {
            var language = new FakeSettingsLanguage(Two) { DeviceLanguage = "tr" };
            var presenter = Start(languages: new List<ISettingsLanguage> { language });
            presenter.Open();
            view.ClickLanguage();
            view.ChooseLanguage("en");

            view.ChooseLanguage(null);

            Assert.That(language.Calls, Is.EqualTo(new[] { "choose en", "device" }));
            Assert.That(view.LanguageRow, Is.EqualTo("TR"));
            Assert.That(view.LanguageSelected, Is.Null);
            Assert.That(view.LanguageCurrentName, Is.EqualTo("Türkçe"));
        }

        [Test]
        public void A_language_chosen_with_the_list_closed_is_ignored()
        {
            var language = new FakeSettingsLanguage(Two);
            var presenter = Start(languages: new List<ISettingsLanguage> { language });
            presenter.Open();

            view.ChooseLanguage("tr");
            view.ChooseLanguage(null);

            Assert.That(language.Calls, Is.Empty);
        }

        [Test]
        public void Close_saves_once_and_hides_the_language_list_and_credits()
        {
            var presenter = Start(languages: new List<ISettingsLanguage> { new FakeSettingsLanguage(Two) },
                credits: new GameCredits("Art by Ada"));
            presenter.Open();
            view.ClickLanguage();
            view.Click(SettingsButton.Credits);

            view.ClickClose();
            view.ClickClose();

            Assert.That(view.LanguagesOpen, Is.False);
            Assert.That(view.LanguagesHidden, Is.EqualTo(1));
            Assert.That(view.CreditsHidden, Is.EqualTo(1));
            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(feedback.Clicks, Is.EqualTo(3), "language, credits, close");
        }

        [Test]
        public void Rapid_open_and_close_shows_and_saves_once_each()
        {
            var presenter = Start();
            presenter.Open();
            presenter.Open();
            view.ClickClose();
            view.ClickClose();

            Assert.That(view.Shown, Is.EqualTo(1));
            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));

            presenter.Open();
            presenter.Dispose();
            view.ClickClose();
            Assert.That(prefs.Saves, Is.EqualTo(1), "a disposed presenter ignores Close");
        }

        // The template ships without Resources/Credits.txt (Compass writes a game's); without it, or with a blank one,
        // and without third-party notices there is no Credits button
        [Test]
        public void Credits_hide_without_credits_or_notices()
        {
            if (Resources.Load<TextAsset>(GameCredits.ResourcePath) == null)
                Assert.That(GameCredits.FromResources().HasText, Is.False, "the template's Resources");
            Assert.That(GameCredits.Read(null).HasText, Is.False, "no file");
            foreach (var blank in new[] { "", " ", "\n\n", " \t\r\n " })
            {
                var blankView = new FakeSettingsView();
                var file = new TextAsset(blank);
                made.Add(file);
                var presenter = Start(blankView, credits: GameCredits.Read(file), config: Config(notices: blank));
                presenter.Open();
                blankView.Click(SettingsButton.Credits);

                Assert.That(blankView.Buttons, Has.No.Member(SettingsButton.Credits), $"blank \"{blank}\"");
                Assert.That(blankView.CreditsShown, Is.Null, $"blank \"{blank}\" opened");
            }
            Assert.That(feedback.Clicks, Is.EqualTo(0));
        }

        // The credits open over the open card (as written, less the blank lines around them), the third-party notices
        // after a blank line; Close goes back to the card
        [Test]
        public void Credits_show_the_game_credits_then_the_notices()
        {
            var file = new TextAsset("\n\"Round UI\" by Ada, https://example.org/round-ui, CC BY 4.0\n\n\"Kenney UI\" by Kenney, CC0 1.0 Universal.\n");
            made.Add(file);
            var presenter = Start(credits: GameCredits.Read(file), config: Config(notices: "\nVContainer: MIT\n"));
            Assert.That(view.Buttons, Has.Member(SettingsButton.Credits));

            presenter.Open();
            view.Click(SettingsButton.Credits);
            Assert.That(view.CreditsShown, Is.EqualTo(
                "\"Round UI\" by Ada, https://example.org/round-ui, CC BY 4.0\n\n\"Kenney UI\" by Kenney, CC0 1.0 Universal.\n\nVContainer: MIT"));
            Assert.That(presenter.IsOpen, Is.True, "the card stays open under the credits");

            view.ClickCreditsClose();
            view.ClickCreditsClose();
            Assert.That(view.CreditsHidden, Is.EqualTo(1), "Close goes back to the card once");
            Assert.That(feedback.Clicks, Is.EqualTo(2));

            var noticesOnly = new FakeSettingsView();
            var notices = Start(noticesOnly, config: Config(notices: "UniTask: MIT"));
            notices.Open();
            noticesOnly.Click(SettingsButton.Credits);
            Assert.That(noticesOnly.CreditsShown, Is.EqualTo("UniTask: MIT"));
        }
    }
}
