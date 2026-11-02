using System.Threading;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class AdPrivacyActionTests
    {
        [Test]
        public void Ad_privacy_follows_the_ump_requirement()
        {
            var privacy = new FakeAdsPrivacy();
            var action = new AdPrivacyAction(privacy);
            var changes = 0;
            action.Changed += () => changes++;

            Assert.That(action.Button, Is.EqualTo(SettingsButton.AdPrivacy));
            Assert.That(action.Visible, Is.False);
            action.PressAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(privacy.Shown, Is.Zero, "not required here");

            privacy.OptionsRequired = true;
            privacy.RaiseChanged();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(action.Visible, Is.True);
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(privacy.Shown, Is.EqualTo(1));
        }
    }
}
