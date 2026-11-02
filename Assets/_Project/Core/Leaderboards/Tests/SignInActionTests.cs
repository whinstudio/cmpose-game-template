using System.Threading;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Leaderboards.Tests
{
    public sealed class SignInActionTests
    {
        [Test]
        public void Sign_in_hides_when_signed_in_or_not_set_up()
        {
            var services = new FakeGameServices();
            var action = new SignInAction(services);
            var changes = 0;
            action.Changed += () => changes++;

            Assert.That(action.Button, Is.EqualTo(SettingsButton.SignIn));
            Assert.That(action.Visible, Is.True, "set up, signed out");
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(services.SignIns, Is.EqualTo(1));

            services.SignedIn = true;
            services.RaiseChanged();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(action.Visible, Is.False, "signed in");
            action.PressAsync(CancellationToken.None).GetAwaiter().GetResult();

            services.SignedIn = false;
            services.Available = false;
            Assert.That(action.Visible, Is.False, "not set up (editor, placeholder IDs)");
            action.PressAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(services.SignIns, Is.EqualTo(1));
        }
    }
}
