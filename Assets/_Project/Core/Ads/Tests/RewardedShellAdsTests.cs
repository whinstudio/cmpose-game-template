using System.Threading;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class RewardedShellAdsTests
    {
        [Test]
        public void Continue_is_on_offer_while_ads_are_available_or_for_premium()
        {
            var ads = new FakeAds { IsAvailable = false };
            var premium = new FakePremium();
            var shell = new RewardedShellAds(ads, premium);

            Assert.That(shell.RewardedAvailable, Is.False);
            premium.IsPremium = true;
            Assert.That(shell.RewardedAvailable && shell.ContinueIsFree, Is.True);
            Assert.That(new RewardedShellAds(new FakeAds(), null).RewardedAvailable, Is.True);
        }

        [Test]
        public void Only_a_watched_or_granted_reward_pays()
        {
            var ads = new FakeAds();
            var shell = new RewardedShellAds(ads, null);

            foreach (var (reply, pays) in new[] { (RewardResult.Rewarded, true), (RewardResult.Granted, true),
                         (RewardResult.Dismissed, false), (RewardResult.NotReady, false), (RewardResult.Failed, false) })
            {
                ads.RewardedReply = reply;
                Assert.That(shell.ShowRewardedAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(pays), reply.ToString());
            }
        }

        [Test]
        public void A_break_asks_for_an_interstitial()
        {
            var ads = new FakeAds();
            new RewardedShellAds(ads, null).ShowBreakAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ads.Interstitials, Is.EqualTo(1));
        }
    }
}
