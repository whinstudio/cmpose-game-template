using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Showcase.Common.Ads
{
    // The ads flag in the showcase: after a crash, Continue (once per run) shows a rewarded ad and continues the run
    // when it pays out. Premium players continue without an ad and the label drops "(ad)";
    // premium comes from IPremiumStatus when a flag registers one (null: nobody is premium). A finished run is a natural
    // break for an interstitial (the pacer decides; GmaAds skips premium). A failed ad counts as not ready. Leaving the
    // scene or finishing the run while the ad is on screen makes its reward change nothing.
    public sealed class ShowcaseContinuePresenter : Presenter<IContinueView>
    {
        private readonly IShowcaseRun run;
        private readonly IAds ads;
        private readonly IPremiumStatus premium;
        private readonly IUiFeedback feedback;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool watching;
        private int finishedRuns;

        public ShowcaseContinuePresenter(IContinueView view, IShowcaseRun run, IAds ads, IPremiumStatus premium, IUiFeedback feedback)
            : base(view)
        {
            this.run = run;
            this.ads = ads;
            this.premium = premium;
            this.feedback = feedback;
        }

        private bool IsPremium => premium != null && premium.IsPremium;

        public override void Start()
        {
            run.Crashed += OnCrashed;
            run.Continued += OnContinued;
            run.Finished += OnFinished;
            View.ContinueClicked += OnContinue;
            if (premium != null) premium.Changed += Refresh;
            View.Hide();
        }

        public override void Dispose()
        {
            run.Crashed -= OnCrashed;
            run.Continued -= OnContinued;
            run.Finished -= OnFinished;
            View.ContinueClicked -= OnContinue;
            if (premium != null) premium.Changed -= Refresh;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void OnCrashed(RunResult result) => Refresh();

        private void OnContinued() => View.Hide();

        private void OnFinished(RunResult result)
        {
            finishedRuns++;
            watching = false;
            View.Hide();
            ads.ShowInterstitialAsync(cancellation.Token).Forget();
        }

        // Continue is on offer while the crashed run may still continue; the label follows premium
        private void Refresh()
        {
            if (watching) return;
            if (run.CanContinue) View.Show(!IsPremium);
            else View.Hide();
        }

        private void OnContinue()
        {
            if (watching || !run.CanContinue) return;
            feedback.Click();
            if (IsPremium) run.Continue();
            else ContinueAsync().Forget();
        }

        private async UniTaskVoid ContinueAsync()
        {
            watching = true;
            var attempt = finishedRuns;
            var token = cancellation.Token;
            RewardResult result;
            try
            {
                result = await ads.ShowRewardedAsync(token);
            }
            catch (OperationCanceledException)
            {
                result = RewardResult.Dismissed;
            }
            catch (Exception)
            {
                result = RewardResult.NotReady;
            }
            if (token.IsCancellationRequested || attempt != finishedRuns) return;
            watching = false;
            switch (result)
            {
                case RewardResult.Rewarded:
                case RewardResult.Granted:
                    run.Continue();
                    break;
                case RewardResult.Dismissed:
                    Refresh();
                    break;
                default:
                    View.ShowHint(ShowcaseKeys.AdNotReady);
                    break;
            }
        }
    }
}
