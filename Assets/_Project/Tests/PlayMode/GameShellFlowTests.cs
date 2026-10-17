using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.Game;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.PlayMode
{
    // The game shell in the real Game scene (its views, its scope): the end cards open, fill and answer at timeScale 0
    public sealed class GameShellFlowTests
    {
        // Frames in a batchmode editor can be long; motion ends on the first frame past its duration
        private const float Slack = 0.3f;

        [TearDown]
        public void Resume() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator Shell_cards_open_while_paused() => UniTask.ToCoroutine(async () =>
        {
            var shell = await OpenGame();

            shell.Begin(ShellMode.Endless);
            shell.SetScore(7);
            Time.timeScale = 0f;

            var complete = Object.FindFirstObjectByType<LevelCompleteCard>();
            var sheet = complete.GetComponentInChildren<KitSheet>(true);
            var stars = complete.GetComponentInChildren<StarRow>(true);
            var reward = complete.transform.Find("Panel/SafeArea/Card/Content/Reward").GetComponent<CountUpText>();
            var won = shell.ShowLevelComplete(2, 3, 120);
            await Until(() => !sheet.Animating && stars.Landed == 3 && !reward.Counting, 4f);
            Assert.That(sheet.IsOpen && sheet.Progress == 1f, Is.True, "the card popped in at timeScale 0");
            Assert.That(stars.Landed, Is.EqualTo(3), "the stars landed at timeScale 0");
            Assert.That(reward.Value, Is.EqualTo(120), "the reward counted up at timeScale 0");
            Press(complete, "Next");
            Assert.That(await Answer(won), Is.EqualTo(EndChoice.Next));
            Assert.That(sheet.gameObject.activeSelf, Is.False);

            var gameOver = Object.FindFirstObjectByType<GameOverCard>();
            var gameOverSheet = gameOver.GetComponentInChildren<KitSheet>(true);
            var score = gameOver.transform.Find("Panel/SafeArea/Card/Content/Score").GetComponent<CountUpText>();
            var lost = shell.ShowGameOver(42, 42, true);
            await Until(() => !gameOverSheet.Animating && !score.Counting, 3f);
            Assert.That(gameOverSheet.Progress, Is.EqualTo(1f));
            Assert.That(score.Value, Is.EqualTo(42));
            Assert.That(gameOver.GetComponentInChildren<Celebration>(true).Playing, Is.True, "the new best still bursts");
            Press(gameOver, "Retry");
            Assert.That(await Answer(lost), Is.EqualTo(EndChoice.Retry));
            Assert.That(Time.timeScale, Is.Zero);
        });

        // The pause button draws under the cards but would show through their dim: it hides while one is open
        [UnityTest]
        public IEnumerator Pause_button_hides_while_a_card_is_open() => UniTask.ToCoroutine(async () =>
        {
            var shell = await OpenGame();
            shell.Begin(ShellMode.Endless);
            var pauseButton = Object.FindFirstObjectByType<PauseView>().GetComponentsInChildren<Button>(true).First(b => b.name == "PauseButton");
            Assert.That(pauseButton.gameObject.activeInHierarchy, Is.True, "the pause button shows during the run");

            var gameOver = Object.FindFirstObjectByType<GameOverCard>();
            var lost = shell.ShowGameOver(5, 9, false);
            Assert.That(pauseButton.gameObject.activeSelf, Is.False, "hidden under the game-over card");
            Press(gameOver, "Retry");
            Assert.That(await Answer(lost), Is.EqualTo(EndChoice.Retry));
            Assert.That(pauseButton.gameObject.activeSelf, Is.True, "back once the card closed");

            // A card replaced by another (or by Begin) leaves it hidden only while one is open
            var failed = shell.ShowLevelFailed(1);
            Assert.That(pauseButton.gameObject.activeSelf, Is.False, "hidden under the failed card");
            shell.Begin(ShellMode.Endless);
            Assert.That(pauseButton.gameObject.activeSelf, Is.True, "back after Begin closed the card");
            Assert.That(failed.Status, Is.EqualTo(UniTaskStatus.Canceled));
        });

        private static async UniTask<IGameShell> OpenGame()
        {
            await SceneManager.LoadSceneAsync("Boot").ToUniTask();
            var menu = await WaitFor<MenuView>(10f);
            Assert.That(menu, Is.Not.Null, "Menu did not open within 10 s");
            menu.GameButtons.First(b => b.GetComponentInChildren<LocalizedText>(true).Key == "menu.play").onClick.Invoke();
            var scope = await WaitFor<GameScope>(10f);
            Assert.That(scope, Is.Not.Null, "Game did not open within 10 s");
            await UniTask.DelayFrame(2);
            return scope.Container.Resolve<IGameShell>();
        }

        private static void Press(Component card, string button)
        {
            var found = card.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == button);
            Assert.That(found, Is.Not.Null, $"no {button} button");
            found.onClick.Invoke();
        }

        private static async UniTask<EndChoice> Answer(UniTask<EndChoice> task)
        {
            var (finished, choice) = await UniTask.WhenAny(task, UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.Realtime));
            Assert.That(finished, Is.True, "the card did not answer within 10 s");
            return choice;
        }

        private static async UniTask Until(Func<bool> done, float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds + Slack;
            while (!done() && Time.realtimeSinceStartup < end) await UniTask.Yield();
        }

        private static async UniTask<T> WaitFor<T>(float seconds) where T : Object
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                var found = Object.FindFirstObjectByType<T>();
                if (found != null) return found;
                await UniTask.Yield();
            }
            return null;
        }
    }
}
