using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Features.Showcase.Common;
// cmpose:ads
using Kturker.UnityBase.Features.Showcase.Common.Ads;
// /cmpose:ads
// cmpose:purchases
using Kturker.UnityBase.Features.Showcase.Common.Purchases;
// /cmpose:purchases
// cmpose:leaderboards
using Kturker.UnityBase.Features.Showcase.Common.Leaderboards;
// /cmpose:leaderboards
// cmpose:saveSystem
using Kturker.UnityBase.Features.Showcase.Common.Save;
// /cmpose:saveSystem

namespace Kturker.UnityBase.Tests.EditMode
{
    // The showcase's shared fakes; the internal 'showcase' flag removes this file with the showcase
    public sealed class FakeShowcaseRun : IShowcaseRun
    {
        private RunResult last;
        public int Continues;
        public int Finishes;
        public int Restarts;
        public bool ContinueUsed;

        public bool IsCrashed { get; private set; }
        public bool CanContinue => IsCrashed && !ContinueUsed;
        public event Action<RunResult> Crashed;
        public event Action Continued;
        public event Action<RunResult> Finished;

        public void Crash(RunResult result)
        {
            last = result;
            IsCrashed = true;
            Crashed?.Invoke(result);
        }

        public void Continue()
        {
            if (!CanContinue) return;
            Continues++;
            ContinueUsed = true;
            IsCrashed = false;
            Continued?.Invoke();
        }

        public void Finish()
        {
            if (!IsCrashed) return;
            Finishes++;
            IsCrashed = false;
            Finished?.Invoke(last);
        }

        public void Restart()
        {
            Restarts++;
            ContinueUsed = false;
        }
    }

    public sealed class FakeGameOverView : IGameOverView
    {
        public bool Visible;
        public int Score;
        public int Best;
        public bool NewBest;
        public event Action RetryClicked;
        public event Action MenuClicked;

        public void Show(int score, int best, bool newBest)
        {
            Visible = true;
            Score = score;
            Best = best;
            NewBest = newBest;
        }

        public void Hide() => Visible = false;
        public void ClickRetry() => RetryClicked?.Invoke();
        public void ClickMenu() => MenuClicked?.Invoke();
    }

    public sealed class FakeShowcaseMenuView : IShowcaseMenuView
    {
        public int? Best;
        public void ShowBest(int best) => Best = best;
    }

    public sealed class FakeShowcaseHudView : IShowcaseHudView
    {
        public int Score = -1;
        public bool HintVisible;
        public void ShowScore(int score) => Score = score;
        public void ShowHint(bool visible) => HintVisible = visible;
    }

    // Shared by both games' tests (the purchases flag's IPremiumStatus paints the runner or the ball gold)
    public sealed class FakePremiumStatus : IPremiumStatus
    {
        public bool IsPremium { get; set; }
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    // cmpose:ads
    public sealed class FakeContinueView : IContinueView
    {
        public bool Visible;
        public bool? ViaAd;
        public string Hint;
        public event Action ContinueClicked;

        public void Show(bool viaAd)
        {
            Visible = true;
            ViaAd = viaAd;
            Hint = null;
        }

        public void Hide()
        {
            Visible = false;
            Hint = null;
        }

        public void ShowHint(string key)
        {
            Visible = false;
            Hint = key;
        }

        public void ClickContinue() => ContinueClicked?.Invoke();
    }
    // /cmpose:ads
    // cmpose:purchases
    public sealed class FakeShopButtonView : IShopButtonView
    {
        public bool? Visible;
        public event Action Clicked;
        public void SetVisible(bool visible) => Visible = visible;
        public void Click() => Clicked?.Invoke();
    }
    // /cmpose:purchases
    // cmpose:leaderboards
    public sealed class FakeShowcaseScoresView : IShowcaseScoresView
    {
        public string Hint;
        public int HintsHidden;
        public event Action LeaderboardClicked;
        public event Action AchievementsClicked;
        public void ShowHint(string key) => Hint = key;

        public void HideHint()
        {
            Hint = null;
            HintsHidden++;
        }

        public void ClickLeaderboard() => LeaderboardClicked?.Invoke();
        public void ClickAchievements() => AchievementsClicked?.Invoke();
    }
    // /cmpose:leaderboards
    // cmpose:saveSystem
    public sealed class FakeRunStatsView : IRunStatsView
    {
        public int? Runs;
        public int? Stars;

        public void Show(int runs, int stars)
        {
            Runs = runs;
            Stars = stars;
        }
    }
    // /cmpose:saveSystem
}
