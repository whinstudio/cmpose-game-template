using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Features.Shell.GameShell;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class FakeShellHudView : IShellHudView
    {
        public bool Shown;
        public int? Level;
        public int Score;
        public readonly List<int> Counted = new List<int>();
        public int? Best;
        public int? Counter;
        public Kturker.UnityBase.Core.Presentation.CurrencyMark CounterIcon;
        public readonly List<string> Toasts = new List<string>();

        public void Show(int? level)
        {
            Shown = true;
            Level = level;
            Score = 0;
            Best = null;
            Counter = null;
        }

        public void SetVisible(bool visible) => Shown = visible;

        public void CountScore(int score)
        {
            Counted.Add(score);
            Score = score;
        }

        public void ShowBest(int best) => Best = best;
        public void ShowCounter(int value) => Counter = value;
        public void ShowCounterIcon(Kturker.UnityBase.Core.Presentation.CurrencyMark mark) => CounterIcon = mark;
        public void Toast(string key) => Toasts.Add(key);
    }

    public sealed class FakeStartHintView : IStartHintView
    {
        public bool Shown;
        public void Show() => Shown = true;
        public void Hide() => Shown = false;
    }

    public abstract class FakeShellCard : IShellCard
    {
        public bool ContinueShown;
        public bool ContinueFree;
        public int Opened;
        public bool Celebrated;
        public event Action<EndChoice> Chosen;
        public bool IsShown { get; protected set; }

        public void ShowContinue(bool shown, bool free)
        {
            ContinueShown = shown;
            ContinueFree = free;
        }

        public void Hide() => IsShown = false;

        public void Choose(EndChoice choice) => Chosen?.Invoke(choice);

        protected void Open()
        {
            Opened++;
            IsShown = true;
            Celebrated = false;
        }
    }

    public sealed class FakeGameOverCard : FakeShellCard, IGameOverCard
    {
        public int Score;
        public int Best;
        public bool Settled;

        public void Show(int score, int best, bool settled)
        {
            Open();
            Score = score;
            Best = best;
            Settled = settled;
        }

        public void CelebrateNewBest() => Celebrated = true;
    }

    public sealed class FakeLevelCompleteCard : FakeShellCard, ILevelCompleteCard
    {
        public int Level;
        public int Stars;
        public int Reward;
        public bool Settled;
        public event Action StarFilled;

        public void Show(int level, int stars, int reward, bool settled)
        {
            Open();
            Level = level;
            Stars = stars;
            Reward = reward;
            Settled = settled;
        }

        public void Celebrate() => Celebrated = true;

        public void FillStar() => StarFilled?.Invoke();
    }

    public sealed class FakeLevelFailedCard : FakeShellCard, ILevelFailedCard
    {
        public int Level;

        public void Show(int level)
        {
            Open();
            Level = level;
        }
    }

    public sealed class FakeShellAds : IShellAds
    {
        public bool RewardedAvailable { get; set; } = true;
        public bool ContinueIsFree { get; set; }
        public bool Earns = true;
        public int Rewardeds;
        public int Breaks;
        // When set, ShowRewardedAsync waits for it (the ad is on screen)
        public UniTaskCompletionSource<bool> PendingRewarded;

        public UniTask<bool> ShowRewardedAsync(CancellationToken cancellation)
        {
            Rewardeds++;
            return PendingRewarded != null ? PendingRewarded.Task : UniTask.FromResult(Earns);
        }

        public UniTask ShowBreakAsync(CancellationToken cancellation)
        {
            Breaks++;
            return UniTask.CompletedTask;
        }
    }

    // In-memory level progress (LevelProgress over a save store is tested in Core/Save/Tests)
    public sealed class FakeLevelProgress : ILevelProgress
    {
        public readonly Dictionary<int, int> Stars = new Dictionary<int, int>();
        public bool Available => true;
        public bool Started { get; set; }
        public int Current { get; set; } = 1;
        public int StarsOf(int level) => Stars.TryGetValue(level, out var stars) ? stars : 0;
        public void Start() => Started = true;

        public void Complete(int level, int stars)
        {
            Started = true;
            Stars[level] = Math.Max(StarsOf(level), stars);
            if (level >= Current) Current = level + 1;
        }
    }

    // A shell over fakes; strict as in the editor and not an automation run unless told otherwise
    public sealed class ShellRig
    {
        public readonly FakeShellHudView Hud = new FakeShellHudView();
        public readonly FakeStartHintView Hint = new FakeStartHintView();
        public readonly FakeGameOverCard GameOver = new FakeGameOverCard();
        public readonly FakeLevelCompleteCard LevelComplete = new FakeLevelCompleteCard();
        public readonly FakeLevelFailedCard LevelFailed = new FakeLevelFailedCard();
        public readonly FakeUiFeedback Feedback = new FakeUiFeedback();
        public readonly FakeLog Log = new FakeLog();
        public readonly ILevelProgress Progress;
        public readonly IShellAds Ads;
        public readonly GameShell Shell;

        public ShellRig(ILevelProgress progress = null, IShellAds ads = null, bool strict = true, ShellRunEvents runs = null,
            Func<bool> automated = null)
        {
            Progress = progress ?? new FakeLevelProgress();
            Ads = ads ?? new NoShellAds();
            Shell = new GameShell(Hud, Hint, GameOver, LevelComplete, LevelFailed, Progress, Ads, Feedback, Log, strict, runs,
                automated);
        }
    }
}
