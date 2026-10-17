using System.IO;
using Kturker.UnityBase.Features.Shell.GameShell;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Save.Tests
{
    public sealed class LevelProgressTests
    {
        private static FileSaveStore Store(FakeFileSystem files) => new FileSaveStore(files, "/save", new FakeLog());

        [Test]
        public void Complete_level_stores_stars_and_advances()
        {
            var files = new FakeFileSystem();
            var rig = new ShellRig(new LevelProgress(Store(files)));
            rig.Shell.Begin(ShellMode.Levels);
            rig.Shell.CompleteLevel(1, 2);
            rig.Shell.CompleteLevel(2, 3);
            rig.Shell.CompleteLevel(1, 1);

            var reread = new LevelProgress(Store(files));
            Assert.That(reread.Started, Is.True);
            Assert.That(reread.Current, Is.EqualTo(3));
            Assert.That(reread.StarsOf(1), Is.EqualTo(2), "a worse replay keeps the best stars");
            Assert.That(reread.StarsOf(2), Is.EqualTo(3));
            Assert.That(reread.StarsOf(3), Is.Zero);
            Assert.That(files.Files.ContainsKey(Path.Combine("/save", "levels.json")), Is.True, "slot 'levels'");
        }

        [Test]
        public void Nothing_is_saved_until_the_level_flow_starts()
        {
            var files = new FakeFileSystem();
            var progress = new LevelProgress(Store(files));

            Assert.That(progress.Available, Is.True);
            Assert.That(progress.Started, Is.False, "an endless game never shows Level N in the menu");
            Assert.That(progress.Current, Is.EqualTo(1));
            Assert.That(files.Files, Is.Empty);
            progress.Start();
            Assert.That(new LevelProgress(Store(files)).Started, Is.True);
        }

        [Test]
        public void Replaying_an_earlier_level_does_not_move_the_current_one_back()
        {
            var progress = new LevelProgress(Store(new FakeFileSystem()));
            progress.Complete(1, 3);
            progress.Complete(2, 3);
            progress.Complete(1, 3);

            Assert.That(progress.Current, Is.EqualTo(3));
        }

        [Test]
        public void A_damaged_save_reads_as_level_one_with_stars_in_range()
        {
            var files = new FakeFileSystem();
            files.Files[Path.Combine("/save", "levels.json")] = "{\"version\":1,\"data\":{\"current\":-4,\"stars\":[9,-2]}}";
            var progress = new LevelProgress(Store(files));

            Assert.That(progress.Current, Is.EqualTo(1));
            Assert.That(progress.StarsOf(1), Is.EqualTo(3));
            Assert.That(progress.StarsOf(2), Is.Zero);
            Assert.That(progress.StarsOf(0), Is.Zero);
        }
    }
}
