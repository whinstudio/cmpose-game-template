using System.Collections.Generic;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class SettingsPresenterTests
    {
        private FakePreferences prefs;
        private FakeSettingsView view;
        private FakeUiFeedback feedback;
        private AudioPreferences audio;
        private HapticsService haptics;
        private SettingsPresenter presenter;
        private FakeReviewRequester requester;
        private FakeSection theme;

        [SetUp]
        public void Create()
        {
            prefs = new FakePreferences();
            view = new FakeSettingsView();
            feedback = new FakeUiFeedback();
            audio = new AudioPreferences(prefs);
            haptics = new HapticsService(prefs, new FakeVibrator());
            requester = new FakeReviewRequester();
            var review = new ReviewPrompter(prefs, requester, new StoreIdentity(StorePlatform.Android, "com.example.game", ""), new FakeLog());
            theme = new FakeSection("settings.theme", SettingsOption.Key("theme.system"), SettingsOption.Key("theme.light"), SettingsOption.Text("Dark!"));
            presenter = new SettingsPresenter(view, audio, haptics, prefs, feedback, review, new List<ISettingsSection> { theme });
            presenter.Start();
        }

        [Test]
        public void Open_shows_the_stored_values_and_changes_write_through_without_saving()
        {
            presenter.Open();

            Assert.That(view.Shown, Is.EqualTo(1));
            Assert.That(view.Music, Is.EqualTo(0.8f));
            Assert.That(view.Sfx, Is.EqualTo(1f));
            Assert.That(view.Vibration, Is.True);

            view.ChangeMusic(0.3f);
            view.ChangeSfx(0.6f);
            view.ChangeVibration(false);

            Assert.That(audio.Music, Is.EqualTo(0.3f));
            Assert.That(audio.Sfx, Is.EqualTo(0.6f));
            Assert.That(haptics.Enabled, Is.False);
            Assert.That(prefs.Saves, Is.EqualTo(0));
        }

        [Test]
        public void Close_hides_saves_once_and_clicks_until_disposed()
        {
            presenter.Open();
            view.ClickClose();

            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
            Assert.That(presenter.IsOpen, Is.False);

            presenter.Open();
            presenter.Dispose();
            view.ClickClose();
            view.ChangeSfx(0.1f);

            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(audio.Sfx, Is.EqualTo(1f));
        }

        [Test]
        public void Rapid_open_and_close_shows_and_saves_once_each()
        {
            presenter.Open();
            presenter.Open();
            view.ClickClose();
            view.ClickClose();

            Assert.That(view.Shown, Is.EqualTo(1));
            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Rate_the_app_opens_the_play_store_page_with_feedback()
        {
            Assert.That(view.RateVisible, Is.True);

            view.ClickRate();

            Assert.That(requester.StorePages, Is.EqualTo(new[]
            {
                ("market://details?id=com.example.game", "https://play.google.com/store/apps/details?id=com.example.game")
            }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Rate_the_app_is_hidden_on_ios_without_an_app_store_id()
        {
            var iosView = new FakeSettingsView();
            var ios = new ReviewPrompter(prefs, requester, new StoreIdentity(StorePlatform.Ios, "com.example.game", ""), new FakeLog());

            new SettingsPresenter(iosView, audio, haptics, prefs, feedback, ios, new List<ISettingsSection>()).Start();

            Assert.That(iosView.RateVisible, Is.False);
        }

        [Test]
        public void Sections_render_on_start_and_a_click_moves_to_the_next_option_with_a_click()
        {
            Assert.That(view.Rows.Count, Is.EqualTo(1));
            Assert.That(view.Rows[0].TitleKey, Is.EqualTo("settings.theme"));
            Assert.That(view.Rows[0].Value.LabelKey, Is.EqualTo("theme.system"));

            view.ClickSection(0);
            Assert.That(theme.Selections, Is.EqualTo(new[] { 1 }));
            Assert.That(view.Rows[0].Value.LabelKey, Is.EqualTo("theme.light"));
            view.ClickSection(0);
            Assert.That(view.Rows[0].Value.Literal, Is.EqualTo("Dark!"));
            view.ClickSection(0);
            Assert.That(theme.Selections, Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(feedback.Clicks, Is.EqualTo(3));
        }

        [Test]
        public void An_out_of_range_click_or_selection_neither_crashes_nor_selects()
        {
            view.ClickSection(5);
            view.ClickSection(-1);
            Assert.That(theme.Selections, Is.Empty);
            Assert.That(feedback.Clicks, Is.EqualTo(0));

            theme.Selected = 7;
            theme.Select(7);
            Assert.That(view.Rows[0].Value.LabelKey, Is.EqualTo("theme.system"));
            view.ClickSection(0);
            Assert.That(theme.Selected, Is.EqualTo(2));
        }

        [Test]
        public void A_disposed_presenter_ignores_section_changes_and_clicks()
        {
            var renders = view.SectionRenders;
            presenter.Dispose();
            theme.Select(1);
            view.ClickSection(0);
            Assert.That(view.SectionRenders, Is.EqualTo(renders));
            Assert.That(theme.Selections, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void A_section_without_options_is_hidden_and_the_rows_below_it_move_up()
        {
            var hidden = new FakeSection("settings.hidden");
            var rowsView = new FakeSettingsView();
            var review = new ReviewPrompter(prefs, requester, new StoreIdentity(StorePlatform.Android, "com.example.game", ""), new FakeLog());
            new SettingsPresenter(rowsView, audio, haptics, prefs, feedback, review, new List<ISettingsSection> { hidden, theme }).Start();

            Assert.That(rowsView.Rows.Count, Is.EqualTo(1));
            Assert.That(rowsView.Rows[0].TitleKey, Is.EqualTo("settings.theme"));
            rowsView.ClickSection(0);
            Assert.That(theme.Selections, Is.EqualTo(new[] { 1 }));
            Assert.That(hidden.Selections, Is.Empty);
        }

        // The menu offers its About panel: the settings panel's last row closes the panel (saving once) and opens About
        [Test]
        public void About_opens_from_settings()
        {
            var about = new FakeAboutPanel();
            Assert.That(view.AboutVisible, Is.False, "no About row before the scene offers one");

            presenter.OfferAbout(about);
            Assert.That(view.AboutVisible, Is.True, "About row");
            view.ClickAbout();
            Assert.That(about.Opened, Is.EqualTo(0), "a closed panel ignores the row");

            presenter.Open();
            view.ClickAbout();

            Assert.That(about.Opened, Is.EqualTo(1));
            Assert.That(presenter.IsOpen, Is.False, "settings closed");
            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        // Game scenes have no About panel: the row stays hidden and pressing it does nothing
        [Test]
        public void About_row_stays_hidden_without_an_about_panel()
        {
            presenter.Open();
            view.ClickAbout();

            Assert.That(view.AboutVisible, Is.False);
            Assert.That(presenter.IsOpen, Is.True);
            Assert.That(feedback.Clicks, Is.EqualTo(0));
        }
    }
}
