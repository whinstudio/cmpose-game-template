using System;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Ads;
using Kturker.UnityBase.Core.Ads.Tests;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Features.Showcase.Common;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Showcase.Common.Ads.Tests
{
    public sealed class ShowcaseContinuePresenterTests
    {
        private static readonly RunResult Result = new RunResult(42, 40, 0);

        private FakeShowcaseRun run;
        private FakeContinueView view;
        private FakeAds ads;
        private FakeUiFeedback feedback;

        [SetUp]
        public void SetUp()
        {
            run = new FakeShowcaseRun();
            view = new FakeContinueView();
            ads = new FakeAds();
            feedback = new FakeUiFeedback();
        }

        private ShowcaseContinuePresenter Create(IPremiumStatus premium = null)
        {
            var presenter = new ShowcaseContinuePresenter(view, run, ads, premium, feedback);
            presenter.Start();
            return presenter;
        }

        [Test]
        public void Nothing_shows_before_a_crash()
        {
            view.Visible = true;
            Create();

            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Without_a_premium_status_the_label_mentions_the_ad()
        {
            Create();

            run.Crash(Result);

            Assert.That((view.Visible, view.ViaAd), Is.EqualTo((true, (bool?)true)));
        }

        [Test]
        public void Premium_players_see_continue_without_the_ad_label()
        {
            Create(new FakePremium { IsPremium = true });

            run.Crash(Result);

            Assert.That((view.Visible, view.ViaAd), Is.EqualTo((true, (bool?)false)));
        }

        [Test]
        public void Premium_bought_while_crashed_relabels_at_once()
        {
            var premium = new FakePremium();
            Create(premium);
            run.Crash(Result);

            premium.IsPremium = true;
            premium.Raise();

            Assert.That((view.Visible, view.ViaAd), Is.EqualTo((true, (bool?)false)));
        }

        [Test]
        public void A_rewarded_ad_continues_the_run()
        {
            Create();
            run.Crash(Result);

            view.ClickContinue();

            Assert.That((ads.Rewardeds, run.Continues, feedback.Clicks), Is.EqualTo((1, 1, 1)));
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Premium_continues_the_run_without_an_ad()
        {
            Create(new FakePremium { IsPremium = true });
            run.Crash(Result);

            view.ClickContinue();

            Assert.That((ads.Rewardeds, run.Continues), Is.EqualTo((0, 1)));
        }

        [Test]
        public void A_dismissed_ad_keeps_continue_on_offer()
        {
            ads.RewardedReply = RewardResult.Dismissed;
            Create();
            run.Crash(Result);

            view.ClickContinue();

            Assert.That(run.Continues, Is.Zero);
            Assert.That((view.Visible, view.ViaAd), Is.EqualTo((true, (bool?)true)));
        }

        [Test]
        public void An_ad_that_is_not_ready_shows_the_hint_and_the_run_does_not_continue()
        {
            ads.RewardedReply = RewardResult.NotReady;
            Create();
            run.Crash(Result);

            view.ClickContinue();

            Assert.That(run.Continues, Is.Zero);
            Assert.That((view.Visible, view.Hint), Is.EqualTo((false, ShowcaseKeys.AdNotReady)));
        }

        [Test]
        public void Only_one_continue_per_run()
        {
            Create();
            run.Crash(Result);
            view.ClickContinue();

            run.Crash(Result);
            view.ClickContinue();

            Assert.That(view.Visible, Is.False);
            Assert.That((ads.Rewardeds, run.Continues), Is.EqualTo((1, 1)));
        }

        [Test]
        public void A_second_tap_while_the_ad_is_on_screen_is_ignored()
        {
            ads.PendingRewarded = new UniTaskCompletionSource<RewardResult>();
            Create();
            run.Crash(Result);

            view.ClickContinue();
            view.ClickContinue();
            ads.PendingRewarded.TrySetResult(RewardResult.Rewarded);

            Assert.That((ads.Rewardeds, run.Continues, feedback.Clicks), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void A_failing_ad_shows_the_hint_and_continue_can_be_tried_again()
        {
            ads.PendingRewarded = new UniTaskCompletionSource<RewardResult>();
            Create();
            run.Crash(Result);
            view.ClickContinue();

            ads.PendingRewarded.TrySetException(new InvalidOperationException("ad failed"));
            ads.PendingRewarded = null;

            Assert.That((run.Continues, view.Hint), Is.EqualTo((0, ShowcaseKeys.AdNotReady)));
            view.ClickContinue();
            Assert.That((ads.Rewardeds, run.Continues), Is.EqualTo((2, 1)));
        }

        [Test]
        public void A_reward_that_arrives_after_the_run_finished_is_ignored()
        {
            ads.PendingRewarded = new UniTaskCompletionSource<RewardResult>();
            Create();
            run.Crash(Result);
            view.ClickContinue();

            run.Finish();
            run.Restart();
            run.Crash(Result);
            ads.PendingRewarded.TrySetResult(RewardResult.Rewarded);

            Assert.That(run.Continues, Is.Zero);
            Assert.That((view.Visible, view.ViaAd), Is.EqualTo((true, (bool?)true)));
        }

        [Test]
        public void A_finished_run_offers_an_interstitial()
        {
            Create();
            run.Crash(Result);

            run.Finish();

            Assert.That(ads.Interstitials, Is.EqualTo(1));
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Leaving_the_scene_ignores_a_late_reward()
        {
            ads.PendingRewarded = new UniTaskCompletionSource<RewardResult>();
            var presenter = Create();
            run.Crash(Result);
            view.ClickContinue();

            presenter.Dispose();
            ads.PendingRewarded.TrySetResult(RewardResult.Rewarded);

            Assert.That(run.Continues, Is.Zero);
        }
    }
}
