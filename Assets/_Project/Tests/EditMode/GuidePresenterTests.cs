using Kturker.UnityBase.App.Game;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class GuidePresenterTests
    {
        [Test]
        public void Shows_until_got_it_and_remembers_the_dismissal()
        {
            var prefs = new FakePreferences();
            var view = new FakeGuideView();
            var feedback = new FakeUiFeedback();
            new GuidePresenter(view, prefs, feedback, enabled: true).Start();
            Assert.That(view.Shown, Is.EqualTo(1));

            view.ClickDismiss();
            view.ClickDismiss();

            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(prefs.Values[GuidePresenter.DismissedKey], Is.EqualTo(true));
            Assert.That(prefs.Saves, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void A_dismissed_guide_stays_hidden()
        {
            var prefs = new FakePreferences();
            prefs.SetBool(GuidePresenter.DismissedKey, true);
            var view = new FakeGuideView();

            new GuidePresenter(view, prefs, new FakeUiFeedback(), enabled: true).Start();

            Assert.That(view.Shown, Is.EqualTo(0));
        }

        [Test]
        public void Disabled_NeverShowsAndWritesNothing()
        {
            var prefs = new FakePreferences();
            var view = new FakeGuideView();

            new GuidePresenter(view, prefs, new FakeUiFeedback(), enabled: false).Start();
            view.ClickDismiss();

            Assert.That(view.Shown, Is.EqualTo(0));
            Assert.That(prefs.Values.ContainsKey(GuidePresenter.DismissedKey), Is.False);
            Assert.That(prefs.Saves, Is.EqualTo(0));
        }
    }
}
