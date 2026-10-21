using System;
using System.IO;
using Kturker.UnityBase.Core.Domain;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class AutomationRunTests
    {
        private string directory;

        [SetUp]
        public void CreateDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "cmpose-automation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void DeleteDirectory()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void Is_not_active_without_a_flag_file()
        {
            Assert.That(AutomationRun.IsActiveIn(directory), Is.False);
        }

        [Test]
        public void Is_active_with_the_autoplay_flag_file()
        {
            File.WriteAllText(Path.Combine(directory, "compass-autoplay"), "");

            Assert.That(AutomationRun.IsActiveIn(directory), Is.True);
        }

        [Test]
        public void Is_active_with_the_capture_flag_file()
        {
            File.WriteAllText(Path.Combine(directory, "compass-capture"), "");

            Assert.That(AutomationRun.IsActiveIn(directory), Is.True);
        }

        [Test]
        public void Names_the_flag_files_compass_writes()
        {
            Assert.That(AutomationRun.FlagFile, Is.EqualTo("compass-autoplay"));
            Assert.That(AutomationRun.CaptureFlagFile, Is.EqualTo("compass-capture"));
        }
    }
}
