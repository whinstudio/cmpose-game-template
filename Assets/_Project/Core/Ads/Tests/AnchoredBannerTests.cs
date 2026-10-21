using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    // Google's anchored adaptive sizes: the device check's 1080 x 2400 phone (density 2.625) got a 168 px banner
    public sealed class AnchoredBannerTests
    {
        [TestCase(320, 50)]
        [TestCase(360, 56)]
        [TestCase(393, 61)]
        [TestCase(411, 64)]
        [TestCase(480, 68)]
        [TestCase(600, 77)]
        [TestCase(640, 81)]
        [TestCase(700, 87)]
        [TestCase(1024, 90)]
        public void A_phone_or_tablet_width_gives_googles_height(int widthDp, int heightDp)
        {
            Assert.That(AnchoredBanner.HeightDp(widthDp, 1366f), Is.EqualTo(heightDp));
        }

        [Test]
        public void A_short_screen_caps_the_banner_at_fifteen_percent_of_its_height_but_never_under_fifty()
        {
            Assert.That(AnchoredBanner.HeightDp(411, 360f), Is.EqualTo(54));
            Assert.That(AnchoredBanner.HeightDp(411, 300f), Is.EqualTo(50));
        }

        [Test]
        public void Pixels_follow_the_device_scale()
        {
            Assert.That(AnchoredBanner.HeightPixels(411, 914f, 2.625f), Is.EqualTo(168));
            Assert.That(AnchoredBanner.HeightPixels(393, 852f, 3f), Is.EqualTo(183));
        }

        [Test]
        public void An_unknown_width_or_scale_gives_no_height()
        {
            Assert.That(AnchoredBanner.HeightPixels(0, 914f, 2.625f), Is.Zero);
            Assert.That(AnchoredBanner.HeightPixels(411, 914f, 0f), Is.Zero);
        }
    }
}
