using System;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class GameShellTests
    {
        private static EndChoice Result(UniTask<EndChoice> task)
        {
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the card's task has not finished");
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void Endless_score_counts_up()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Endless);
            rig.Shell.SetScore(5);
            rig.Shell.SetScore(12);
            rig.Shell.SetBest(40);

            Assert.That(rig.Hud.Shown, Is.True);
            Assert.That(rig.Hud.Level, Is.Null, "no level in the endless HUD");
            Assert.That(rig.Hud.Counted, Is.EqualTo(new[] { 5, 12 }), "the score counts to each value, never jumps");
            Assert.That(rig.Hud.Best, Is.EqualTo(40));
            Assert.That(rig.Hud.Counter, Is.Null, "the counter stays hidden until the first SetCounter");
            rig.Shell.SetCounter(3);
            Assert.That(rig.Hud.Counter, Is.EqualTo(3));
        }

        [Test]
        public void Begin_starts_from_zero_and_toasts_reach_the_hud()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Endless);
            rig.Shell.SetScore(9);
            rig.Shell.Toast(ShellKeys.Perfect);
            rig.Shell.Toast("");
            rig.Shell.Begin(ShellMode.Endless);

            Assert.That(rig.Hud.Score, Is.Zero);
            Assert.That(rig.Hud.Toasts, Is.EqualTo(new[] { ShellKeys.Perfect }), "an empty key shows nothing");
        }

        [Test]
        public void New_best_celebrates()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Endless);
            rig.Shell.ShowStartHint();

            var task = rig.Shell.ShowGameOver(42, 42, true);

            Assert.That(rig.GameOver.IsShown, Is.True);
            Assert.That(rig.GameOver.Score, Is.EqualTo(42));
            Assert.That(rig.GameOver.Celebrated, Is.True);
            Assert.That(rig.Feedback.Successes, Is.EqualTo(1));
            Assert.That(rig.Hint.Shown, Is.False, "an end card hides the start hint");
            Assert.That(rig.Hud.Shown, Is.False, "and the HUD under it");
            rig.GameOver.Choose(EndChoice.Retry);
            Assert.That(Result(task), Is.EqualTo(EndChoice.Retry));
            Assert.That(rig.GameOver.IsShown, Is.False);
        }

        [Test]
        public void A_plain_game_over_does_not_celebrate()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Endless);

            var task = rig.Shell.ShowGameOver(10, 42, false);

            Assert.That(rig.GameOver.Celebrated, Is.False);
            Assert.That(rig.Feedback.Successes, Is.Zero);
            Assert.That(rig.GameOver.Best, Is.EqualTo(42));
            rig.GameOver.Choose(EndChoice.Menu);
            Assert.That(Result(task), Is.EqualTo(EndChoice.Menu));
            Assert.That(rig.Feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Levels_require_save_store()
        {
            var rig = new ShellRig(new NoLevelProgress());

            Assert.Throws<InvalidOperationException>(() => rig.Shell.Begin(ShellMode.Levels));
            Assert.Throws<InvalidOperationException>(() => rig.Shell.CompleteLevel(1, 3));
            Assert.That(rig.Shell.CurrentLevel, Is.EqualTo(1));
        }

        [Test]
        public void Levels_without_a_save_store_log_and_fall_back_to_endless_on_a_device()
        {
            var rig = new ShellRig(new NoLevelProgress(), strict: false);

            rig.Shell.Begin(ShellMode.Levels);
            rig.Shell.CompleteLevel(1, 3);

            Assert.That(rig.Shell.Mode, Is.EqualTo(ShellMode.Endless));
            Assert.That(rig.Hud.Shown, Is.True);
            Assert.That(rig.Hud.Level, Is.Null);
            Assert.That(rig.Log.Lines.FindAll(l => l.StartsWith("ERROR") && l.Contains("saveSystem")).Count, Is.EqualTo(2));
        }

        [Test]
        public void Complete_level_stores_stars_and_advances()
        {
            var progress = new FakeLevelProgress();
            var rig = new ShellRig(progress);
            rig.Shell.Begin(ShellMode.Levels);

            Assert.That(progress.Started, Is.True, "Begin(Levels) starts the flow, so the menu shows the level");
            Assert.That(rig.Hud.Level, Is.EqualTo(1));
            rig.Shell.CompleteLevel(1, 2);
            rig.Shell.CompleteLevel(1, 7);
            rig.Shell.CompleteLevel(2, -1);

            Assert.That(progress.StarsOf(1), Is.EqualTo(3), "stars are clamped to 0-3 and the best is kept");
            Assert.That(progress.StarsOf(2), Is.Zero);
            Assert.That(rig.Shell.CurrentLevel, Is.EqualTo(3));
            rig.Shell.Begin(ShellMode.Levels);
            Assert.That(rig.Hud.Level, Is.EqualTo(3));
        }

        [Test]
        public void Level_complete_fills_stars_and_offers_next()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Levels);

            var task = rig.Shell.ShowLevelComplete(4, 5, 120);
            rig.LevelComplete.FillStar();

            Assert.That(rig.LevelComplete.Level, Is.EqualTo(4));
            Assert.That(rig.LevelComplete.Stars, Is.EqualTo(3), "at most three stars");
            Assert.That(rig.LevelComplete.Reward, Is.EqualTo(120));
            Assert.That(rig.LevelComplete.Celebrated, Is.True);
            Assert.That(rig.LevelComplete.ContinueShown, Is.False, "nothing to continue after a win");
            Assert.That(rig.Feedback.Impacts, Is.EqualTo(new[] { HapticStrength.Light }), "each star is felt");
            rig.LevelComplete.Choose(EndChoice.Next);
            Assert.That(Result(task), Is.EqualTo(EndChoice.Next));
        }

        [Test]
        public void Continue_hidden_without_rewarded()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Levels);

            var failed = rig.Shell.ShowLevelFailed(2);
            Assert.That(rig.LevelFailed.ContinueShown, Is.False);
            rig.LevelFailed.Choose(EndChoice.Continue);
            Assert.That(failed.Status, Is.EqualTo(UniTaskStatus.Pending), "a stray Continue press does nothing");
            rig.LevelFailed.Choose(EndChoice.Retry);
            Assert.That(Result(failed), Is.EqualTo(EndChoice.Retry));

            var ads = new FakeShellAds { RewardedAvailable = false };
            var offline = new ShellRig(ads: ads);
            offline.Shell.Begin(ShellMode.Endless);
            offline.Shell.ShowGameOver(1, 2, false).Forget();
            Assert.That(offline.GameOver.ContinueShown, Is.False, "no rewarded ad ready");
        }

        [Test]
        public void Continue_shows_a_rewarded_ad_once_per_run()
        {
            var ads = new FakeShellAds();
            var rig = new ShellRig(ads: ads);
            rig.Shell.Begin(ShellMode.Endless);

            var first = rig.Shell.ShowGameOver(10, 20, false);
            Assert.That(rig.GameOver.ContinueShown, Is.True);
            rig.GameOver.Choose(EndChoice.Continue);
            Assert.That(Result(first), Is.EqualTo(EndChoice.Continue));
            Assert.That(ads.Rewardeds, Is.EqualTo(1));
            Assert.That(ads.Breaks, Is.Zero, "no break before the run goes on");
            Assert.That(rig.Hud.Shown, Is.True, "the HUD is back for the rest of the run");

            var second = rig.Shell.ShowGameOver(15, 20, false);
            Assert.That(rig.GameOver.ContinueShown, Is.False, "one continue per run");
            rig.GameOver.Choose(EndChoice.Retry);
            Assert.That(Result(second), Is.EqualTo(EndChoice.Retry));
            Assert.That(ads.Breaks, Is.EqualTo(1), "a finished run is a break for an interstitial");

            rig.Shell.Begin(ShellMode.Endless);
            rig.Shell.ShowGameOver(3, 20, false).Forget();
            Assert.That(rig.GameOver.ContinueShown, Is.True, "a new run offers it again");
        }

        [Test]
        public void A_rewarded_ad_that_does_not_pay_keeps_the_card_and_presses_wait_for_the_ad()
        {
            var ads = new FakeShellAds { PendingRewarded = new UniTaskCompletionSource<bool>() };
            var rig = new ShellRig(ads: ads);
            rig.Shell.Begin(ShellMode.Endless);

            var task = rig.Shell.ShowGameOver(10, 20, false);
            rig.GameOver.Choose(EndChoice.Continue);
            rig.GameOver.Choose(EndChoice.Menu);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "presses while the ad is on screen are ignored");
            Assert.That(rig.GameOver.ContinueShown, Is.False);
            ads.PendingRewarded.TrySetResult(false);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(rig.GameOver.IsShown, Is.True);
            Assert.That(rig.GameOver.ContinueShown, Is.True, "offered again while an ad is available");
            rig.GameOver.Choose(EndChoice.Menu);
            Assert.That(Result(task), Is.EqualTo(EndChoice.Menu));
        }

        [Test]
        public void Premium_continue_drops_the_ad_label()
        {
            var rig = new ShellRig(ads: new FakeShellAds { ContinueIsFree = true });
            rig.Shell.Begin(ShellMode.Endless);
            rig.Shell.ShowGameOver(1, 2, false).Forget();

            Assert.That(rig.GameOver.ContinueShown && rig.GameOver.ContinueFree, Is.True);
        }

        [Test]
        public void A_new_card_or_leaving_the_scene_cancels_the_open_one()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Levels);

            var failed = rig.Shell.ShowLevelFailed(1);
            var complete = rig.Shell.ShowLevelComplete(1, 1, 0);
            Assert.That(failed.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(rig.LevelFailed.IsShown, Is.False);
            rig.LevelFailed.Choose(EndChoice.Retry);
            Assert.That(complete.Status, Is.EqualTo(UniTaskStatus.Pending), "the closed card's buttons do nothing");

            rig.Shell.Dispose();
            Assert.That(complete.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(rig.LevelComplete.IsShown, Is.False);
        }

        [Test]
        public void A_double_press_counts_once()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Endless);
            var task = rig.Shell.ShowGameOver(1, 2, false);

            rig.GameOver.Choose(EndChoice.Retry);
            rig.GameOver.Choose(EndChoice.Menu);

            Assert.That(Result(task), Is.EqualTo(EndChoice.Retry));
            Assert.That(rig.Feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Level_failed_shows_its_level_and_buzzes()
        {
            var rig = new ShellRig();
            rig.Shell.Begin(ShellMode.Levels);
            rig.Shell.ShowLevelFailed(7).Forget();

            Assert.That(rig.LevelFailed.Level, Is.EqualTo(7));
            Assert.That(rig.Feedback.Failures, Is.EqualTo(1));
        }
    }
}
