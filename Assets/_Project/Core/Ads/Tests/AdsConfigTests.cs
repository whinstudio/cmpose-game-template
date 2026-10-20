using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class AdsConfigTests
    {
        [Test]
        public void Unit_ids_have_admob_format_on_both_platforms()
        {
            foreach (var ios in new[] { false, true })
            {
                var u = AdsConfig.Units(ios);
                foreach (var id in new[] { u.Banner, u.Interstitial, u.Rewarded })
                    Assert.That(id, Does.Match(@"^ca-app-pub-\d{16}/\d{10}$"));
            }
        }

        [Test]
        public void Test_ids_are_detected_as_test_ids()
        {
            var test = new AdUnits("ca-app-pub-3940256099942544/9214589741", "ca-app-pub-3940256099942544/1033173712", "ca-app-pub-3940256099942544/5224354917");
            Assert.That(AdsConfig.UsesTestIds(test), Is.True);
        }

        [Test]
        public void Only_googles_test_publisher_counts_as_a_test_id()
        {
            Assert.That(AdsConfig.IsTestId("ca-app-pub-3940256099942544/1033173712"), Is.True);
            Assert.That(AdsConfig.IsTestId("ca-app-pub-1234567890123456/1033173712"), Is.False);
            Assert.That(AdsConfig.IsTestId("ca-app-pub-3940256099942544~3347511713"), Is.False);
            Assert.That(AdsConfig.IsTestId(""), Is.False);
            Assert.That(AdsConfig.IsTestId(null), Is.False);
            Assert.That(AdsConfig.UsesTestIds(new AdUnits("ca-app-pub-1/1", "ca-app-pub-1/2", "ca-app-pub-3940256099942544/5224354917")), Is.True);
            Assert.That(AdsConfig.UsesTestIds(new AdUnits("ca-app-pub-1/1", "ca-app-pub-1/2", "ca-app-pub-1/3")), Is.False);
        }

        [Test]
        public void Interstitial_pacing_is_unchanged()
        {
            Assert.That((AdsConfig.InterstitialStartDelaySeconds, AdsConfig.InterstitialGapSeconds, AdsConfig.InterstitialEvery),
                Is.EqualTo((60, 60, 2)));
        }

        [Test]
        public void Rewarded_ads_are_capped_at_fifteen_a_day() => Assert.That(AdsConfig.RewardedDailyCap, Is.EqualTo(15));

        [Test]
        public void Pacing_and_the_tracking_prompt_have_their_defaults()
        {
            Assert.That(AdsConfig.InterstitialStartDelaySeconds, Is.EqualTo(60));
            Assert.That(AdsConfig.InterstitialGapSeconds, Is.EqualTo(60));
            Assert.That(AdsConfig.InterstitialEvery, Is.EqualTo(2));
            Assert.That(AdsConfig.DebugGeographyEea, Is.False);
            Assert.That(AdsConfig.TrackingUsageDescription, Is.Not.Empty);
        }
    }
}
