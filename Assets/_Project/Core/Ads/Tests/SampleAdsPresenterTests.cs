using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Features.Sample.Ads;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class SampleAdsPresenterTests
    {
        private FakeBonusView view;
        private FakeSampleView sample;
        private FakeAds ads;
        private FakeCompletionCounter counter;
        private FakeUiFeedback feedback;
        private SampleAdsPresenter presenter;

        [SetUp]
        public void Create()
        {
            view = new FakeBonusView();
            sample = new FakeSampleView();
            ads = new FakeAds();
            counter = new FakeCompletionCounter();
            feedback = new FakeUiFeedback();
            presenter = new SampleAdsPresenter(view, sample, ads, counter, feedback);
            presenter.Start();
        }

        [Test]
        public void The_hint_follows_whether_ads_are_available()
        {
            Assert.That(view.Hint, Is.EqualTo(AdsKeys.BonusHint));

            ads.IsAvailable = false;
            ads.RaiseChanged();

            Assert.That(view.Hint, Is.EqualTo(AdsKeys.Unavailable));
        }

        [Test]
        public void Complete_shows_no_full_screen_ad()
        {
            sample.ClickComplete();
            sample.ClickComplete();

            Assert.That(ads.Interstitials, Is.Zero);
        }

        [Test]
        public void A_watched_or_granted_reward_adds_one()
        {
            view.ClickBonus();
            ads.RewardedReply = RewardResult.Granted;
            view.ClickBonus();

            Assert.That(counter.Count, Is.EqualTo(2));
            Assert.That(sample.Completed, Is.EqualTo(2));
            Assert.That(feedback.Clicks, Is.EqualTo(2));
        }

        [Test]
        public void No_reward_without_a_finished_ad_and_a_hint_when_none_is_ready()
        {
            ads.RewardedReply = RewardResult.Dismissed;
            view.ClickBonus();
            Assert.That(view.Hint, Is.EqualTo(AdsKeys.BonusHint));
            ads.RewardedReply = RewardResult.NotReady;
            view.ClickBonus();
            Assert.That(view.Hint, Is.EqualTo(AdsKeys.NotAvailable));
            ads.RewardedReply = RewardResult.Failed;
            view.ClickBonus();
            Assert.That(view.Hint, Is.EqualTo(AdsKeys.NotAvailable));
            ads.IsAvailable = false;
            ads.RewardedReply = RewardResult.NotReady;
            view.ClickBonus();

            Assert.That(view.Hint, Is.EqualTo(AdsKeys.Unavailable));
            Assert.That(counter.Count, Is.Zero);
            Assert.That(sample.Completed, Is.Null);
        }

        [Test]
        public void A_press_while_an_ad_is_open_is_ignored_and_a_late_reward_after_leaving_changes_nothing()
        {
            ads.PendingRewarded = new UniTaskCompletionSource<RewardResult>();
            view.ClickBonus();
            view.ClickBonus();
            Assert.That(ads.Rewardeds, Is.EqualTo(1));

            presenter.Dispose();
            ads.PendingRewarded.TrySetResult(RewardResult.Rewarded);
            sample.ClickComplete();

            Assert.That(counter.Count, Is.Zero);
            Assert.That(ads.Interstitials, Is.Zero);
        }
    }
}
