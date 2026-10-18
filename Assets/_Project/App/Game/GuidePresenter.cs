using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.App.Game
{
    // Developer note: shown only in the Unity Editor, never to players.
    // Shown until "Got it" once; the dismissal is saved at once (guide.dismissed)
    public sealed class GuidePresenter : Presenter<IGuideView>
    {
        public const string DismissedKey = "guide.dismissed";

        private readonly IPreferences preferences;
        private readonly IUiFeedback feedback;
        private readonly bool enabled;
        private bool visible;

        public GuidePresenter(IGuideView view, IPreferences preferences, IUiFeedback feedback, bool enabled) : base(view)
        {
            this.preferences = preferences;
            this.feedback = feedback;
            this.enabled = enabled;
        }

        public override void Start()
        {
            if (!enabled) return;
            View.DismissClicked += OnDismiss;
            if (preferences.GetBool(DismissedKey, false)) return;
            visible = true;
            View.Show();
        }

        public override void Dispose() => View.DismissClicked -= OnDismiss;

        private void OnDismiss()
        {
            if (!visible) return;
            visible = false;
            feedback.Click();
            View.Hide();
            preferences.SetBool(DismissedKey, true);
            preferences.Save();
        }
    }
}
