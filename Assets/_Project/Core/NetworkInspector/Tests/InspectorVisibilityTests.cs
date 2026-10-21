using NUnit.Framework;

namespace Kturker.UnityBase.Core.NetworkInspector.Tests
{
    public sealed class InspectorVisibilityTests
    {
        [Test]
        public void Shows_in_a_development_build()
        {
            Assert.That(InspectorInstaller.ShowInspector(debugBuild: true, automation: false), Is.True);
        }

        [Test]
        public void Hides_in_a_development_build_during_a_compass_automation_run()
        {
            Assert.That(InspectorInstaller.ShowInspector(debugBuild: true, automation: true), Is.False);
        }

        [Test]
        public void Hides_in_a_release_build()
        {
            Assert.That(InspectorInstaller.ShowInspector(debugBuild: false, automation: false), Is.False);
        }
    }
}
