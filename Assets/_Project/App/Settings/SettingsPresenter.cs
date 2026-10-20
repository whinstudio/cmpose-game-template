using System.Collections.Generic;
using Kturker.UnityBase.App.About;
using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;

namespace Kturker.UnityBase.App.Settings
{
    // Sliders and the toggle write through at once; Close saves the preferences once. Opening an open panel or
    // closing a closed one does nothing. "Rate the app" opens the store page (hidden on iOS without an App Store ID).
    // Feature flags add section rows (ISettingsSection; one without options is hidden); pressing a row selects its next option.
    // The last row, About, shows once the scene offers its About panel (the menu): it closes this panel, then opens About.
    public sealed class SettingsPresenter : Presenter<ISettingsView>, ISettingsPanel
    {
        private readonly AudioPreferences audio;
        private readonly HapticsService haptics;
        private readonly IPreferences preferences;
        private readonly IUiFeedback feedback;
        private readonly ReviewPrompter review;
        private readonly IReadOnlyList<ISettingsSection> sections;
        // The sections that have options, in order: the rows the view shows
        private readonly List<ISettingsSection> shown = new List<ISettingsSection>();
        private IAboutPanel about;

        public SettingsPresenter(ISettingsView view, AudioPreferences audio, HapticsService haptics, IPreferences preferences,
            IUiFeedback feedback, ReviewPrompter review, IReadOnlyList<ISettingsSection> sections) : base(view)
        {
            this.audio = audio;
            this.haptics = haptics;
            this.preferences = preferences;
            this.feedback = feedback;
            this.review = review;
            this.sections = sections;
        }

        public bool IsOpen { get; private set; }

        public override void Start()
        {
            View.SetRateVisible(review.CanOpenStorePage);
            View.SetAboutVisible(about != null);
            View.MusicChanged += OnMusic;
            View.SfxChanged += OnSfx;
            View.VibrationChanged += OnVibration;
            View.RateClicked += OnRate;
            View.AboutClicked += OnAbout;
            View.CloseClicked += OnClose;
            foreach (var section in sections) section.Changed += RenderSections;
            View.SectionClicked += OnSection;
            RenderSections();
        }

        public override void Dispose()
        {
            View.MusicChanged -= OnMusic;
            View.SfxChanged -= OnSfx;
            View.VibrationChanged -= OnVibration;
            View.RateClicked -= OnRate;
            View.AboutClicked -= OnAbout;
            View.CloseClicked -= OnClose;
            foreach (var section in sections) section.Changed -= RenderSections;
            View.SectionClicked -= OnSection;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            View.Show(audio.Music, audio.Sfx, haptics.Enabled);
        }

        public void OfferAbout(IAboutPanel panel)
        {
            about = panel;
            View.SetAboutVisible(about != null);
        }

        private void OnMusic(float value) => audio.Music = value;

        private void OnSfx(float value) => audio.Sfx = value;

        private void OnVibration(bool value) => haptics.Enabled = value;

        private void OnRate()
        {
            feedback.Click();
            review.OpenStorePage();
        }

        private void OnClose()
        {
            if (!IsOpen) return;
            feedback.Click();
            Close();
        }

        // About draws under this panel's canvas, so the panel closes (and saves) first
        private void OnAbout()
        {
            if (!IsOpen || about == null) return;
            feedback.Click();
            Close();
            about.Open();
        }

        private void Close()
        {
            IsOpen = false;
            preferences.Save();
            View.Hide();
        }

        private void RenderSections()
        {
            shown.Clear();
            var rows = new List<SettingsRow>(sections.Count);
            foreach (var section in sections)
            {
                var options = section.Options;
                if (options.Count == 0) continue;
                var selected = section.Selected >= 0 && section.Selected < options.Count ? section.Selected : 0;
                shown.Add(section);
                rows.Add(new SettingsRow(section.TitleKey, options[selected]));
            }
            View.ShowSections(rows);
        }

        private void OnSection(int index)
        {
            if (index < 0 || index >= shown.Count) return;
            var section = shown[index];
            if (section.Options.Count == 0) return;
            feedback.Click();
            var current = section.Selected >= 0 ? section.Selected : -1;
            section.Select((current + 1) % section.Options.Count);
        }
    }
}
