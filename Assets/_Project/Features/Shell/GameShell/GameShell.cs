using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // IGameShell over the scene's shell views. One end card at a time, the HUD hidden under it: opening another one (or Begin, or leaving the
    // scene) closes the open one and cancels its task (OperationCanceledException). A press is taken once; presses
    // while a rewarded ad is on screen are ignored. Continue is offered once per run (Begin starts a run) while
    // IShellAds has a rewarded ad; each landing star taps lightly; every other choice ends the run (RunEnded, and the
    // root's ShellRunEvents when given) and then gives IShellAds its break (an interstitial). strict (the editor)
    // throws where a device logs.
    public sealed class GameShell : IGameShell, IShellRunEvents, IDisposable
    {
        public const int MaxStars = 3;

        private readonly IShellHudView hud;
        private readonly IStartHintView hint;
        private readonly IGameOverCard gameOver;
        private readonly ILevelCompleteCard levelComplete;
        private readonly ILevelFailedCard levelFailed;
        private readonly ILevelProgress progress;
        private readonly IShellAds ads;
        private readonly IUiFeedback feedback;
        private readonly ILog log;
        private readonly bool strict;
        private readonly ShellRunEvents runs;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private IShellCard open;
        private UniTaskCompletionSource<EndChoice> pending;
        private int session;
        private bool continued;

        public GameShell(IShellHudView hud, IStartHintView hint, IGameOverCard gameOver, ILevelCompleteCard levelComplete,
            ILevelFailedCard levelFailed, ILevelProgress progress, IShellAds ads, IUiFeedback feedback, ILog log, bool strict,
            ShellRunEvents runs = null)
        {
            this.hud = hud;
            this.hint = hint;
            this.gameOver = gameOver;
            this.levelComplete = levelComplete;
            this.levelFailed = levelFailed;
            this.progress = progress;
            this.ads = ads;
            this.feedback = feedback;
            this.log = log;
            this.strict = strict;
            this.runs = runs;
            levelComplete.StarFilled += OnStarFilled;
        }

        public event Action<bool> RunEnded;

        // After Begin showed the HUD for a new run or level (the economy writes the coins into its counter again)
        public event Action Begun;

        public ShellMode Mode { get; private set; }

        public int CurrentLevel => progress.Available ? Math.Max(1, progress.Current) : 1;

        public void Begin(ShellMode mode)
        {
            if (mode == ShellMode.Levels && !progress.Available)
            {
                const string message = "IGameShell.Begin(Levels) needs a save store: turn the saveSystem flag on";
                if (strict) throw new InvalidOperationException(message);
                log.Error(message);
                mode = ShellMode.Endless;
            }
            Mode = mode;
            continued = false;
            CloseOpenCard();
            if (mode == ShellMode.Levels) progress.Start();
            hud.Show(mode == ShellMode.Levels ? CurrentLevel : (int?)null);
            Begun?.Invoke();
        }

        public void SetScore(int score) => hud.CountScore(score);

        public void SetBest(int best) => hud.ShowBest(best);

        public void SetCounter(int value) => hud.ShowCounter(value);

        public void SetCounterIcon(CurrencyMark mark) => hud.ShowCounterIcon(mark);

        public void Toast(string key)
        {
            if (!string.IsNullOrEmpty(key)) hud.Toast(key);
        }

        public void ShowStartHint() => hint.Show();

        public void HideStartHint() => hint.Hide();

        public UniTask<EndChoice> ShowGameOver(int score, int best, bool newBest)
        {
            var id = Open(gameOver);
            gameOver.Show(score, best);
            if (newBest)
            {
                gameOver.CelebrateNewBest();
                feedback.Success();
            }
            return Choose(gameOver, true, false, id);
        }

        public UniTask<EndChoice> ShowLevelComplete(int level, int stars, int reward)
        {
            var id = Open(levelComplete);
            var shown = Math.Max(0, Math.Min(MaxStars, stars));
            levelComplete.Show(level, shown, Math.Max(0, reward));
            if (shown > 0) levelComplete.Celebrate();
            feedback.Success();
            return Choose(levelComplete, false, true, id);
        }

        public UniTask<EndChoice> ShowLevelFailed(int level)
        {
            var id = Open(levelFailed);
            levelFailed.Show(level);
            feedback.Failure();
            return Choose(levelFailed, true, false, id);
        }

        public void CompleteLevel(int level, int stars)
        {
            if (!progress.Available)
            {
                const string message = "IGameShell.CompleteLevel needs a save store: turn the saveSystem flag on";
                if (strict) throw new InvalidOperationException(message);
                log.Error(message);
                return;
            }
            progress.Complete(Math.Max(1, level), Math.Max(0, Math.Min(MaxStars, stars)));
        }

        public void Dispose()
        {
            levelComplete.StarFilled -= OnStarFilled;
            cancellation.Cancel();
            CloseOpenCard();
            cancellation.Dispose();
        }

        // Closes the card already open (its task is cancelled) and makes this one the open card
        private int Open(IShellCard card)
        {
            CloseOpenCard();
            hint.Hide();
            hud.SetVisible(false);
            open = card;
            card.Chosen += OnChosen;
            return session;
        }

        private void CloseOpenCard()
        {
            session++;
            var waiting = pending;
            pending = null;
            if (open != null)
            {
                open.Chosen -= OnChosen;
                open.Hide();
                open = null;
            }
            waiting?.TrySetCanceled();
        }

        // Each landing star is felt
        private void OnStarFilled() => feedback.Impact(HapticStrength.Light);

        private void OnChosen(EndChoice choice)
        {
            var waiting = pending;
            pending = null;
            waiting?.TrySetResult(choice);
        }

        private async UniTask<EndChoice> Choose(IShellCard card, bool continuable, bool won, int id)
        {
            var token = cancellation.Token;
            var offer = continuable && !continued && ads.RewardedAvailable;
            card.ShowContinue(offer, ads.ContinueIsFree);
            while (true)
            {
                pending = new UniTaskCompletionSource<EndChoice>();
                var choice = await pending.Task;
                if (choice == EndChoice.Continue)
                {
                    if (!offer) continue;
                    feedback.Click();
                    card.ShowContinue(false, false);
                    if (await Rewarded(token, id))
                    {
                        continued = true;
                        Close(card, id);
                        hud.SetVisible(true);
                        return EndChoice.Continue;
                    }
                    offer = ads.RewardedAvailable;
                    card.ShowContinue(offer, ads.ContinueIsFree);
                    continue;
                }
                feedback.Click();
                Close(card, id);
                RunEnded?.Invoke(won);
                runs?.Report(Mode, won);
                await Break(token);
                return choice;
            }
        }

        private async UniTask<bool> Rewarded(CancellationToken token, int id)
        {
            bool earned;
            try
            {
                earned = await ads.ShowRewardedAsync(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                log.Error("shell: the rewarded continue failed", e);
                earned = false;
            }
            // The card was replaced or the scene left while the ad was on screen: the reward changes nothing
            if (token.IsCancellationRequested || id != session) throw new OperationCanceledException();
            return earned;
        }

        private async UniTask Break(CancellationToken token)
        {
            try
            {
                await ads.ShowBreakAsync(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                log.Error("shell: the break ad failed", e);
            }
        }

        private void Close(IShellCard card, int id)
        {
            if (id != session || open != card) return;
            card.Chosen -= OnChosen;
            card.Hide();
            open = null;
        }
    }
}
