using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class PurchasesConfigTests
    {
        private const string TestKey = "test_qmHxQuucnBoXDlJVLUGsCxcGXrw";
        private const string PlatformKey = "goog_AbCdEf123";

        [Test]
        public void A_test_store_key_works_only_in_development_builds()
        {
            Assert.That(PurchasesConfig.CanConfigure(TestKey, debugBuild: true, editor: false), Is.True);
            Assert.That(PurchasesConfig.CanConfigure(TestKey, debugBuild: false, editor: false), Is.False);
        }

        [Test]
        public void A_platform_key_works_in_every_build()
        {
            Assert.That(PurchasesConfig.CanConfigure(PlatformKey, debugBuild: false, editor: false), Is.True);
            Assert.That(PurchasesConfig.CanConfigure(PlatformKey, debugBuild: true, editor: false), Is.True);
        }

        [Test]
        public void The_editor_and_a_blank_key_never_configure()
        {
            Assert.That(PurchasesConfig.CanConfigure(PlatformKey, debugBuild: true, editor: true), Is.False);
            Assert.That(PurchasesConfig.CanConfigure("", debugBuild: true, editor: false), Is.False);
            Assert.That(PurchasesConfig.CanConfigure("  ", debugBuild: true, editor: false), Is.False);
            Assert.That(PurchasesConfig.CanConfigure(null, debugBuild: true, editor: false), Is.False);
        }

        [Test]
        public void Api_keys_have_revenuecat_format_on_both_platforms()
        {
            Assert.That(PurchasesConfig.ApiKey(ios: true), Is.EqualTo(PurchasesConfig.ApiKeyApple));
            Assert.That(PurchasesConfig.ApiKey(ios: false), Is.EqualTo(PurchasesConfig.ApiKeyGoogle));
            foreach (var ios in new[] { false, true })
                Assert.That(PurchasesConfig.ApiKey(ios), Does.Match(@"^(test_|appl_|goog_)[A-Za-z0-9]+$"));
            // A RevenueCat entitlement identifier: one word, no spaces (the template's is "premium")
            Assert.That(PurchasesConfig.PremiumEntitlement, Does.Match(@"^[A-Za-z0-9_.\-]+$"));
        }

        [Test]
        public void Test_store_keys_are_detected_as_test_keys()
        {
            Assert.That(PurchasesConfig.IsTestStoreKey(TestKey), Is.True);
            Assert.That(PurchasesConfig.IsTestStoreKey(PlatformKey), Is.False);
            Assert.That(PurchasesConfig.IsTestStoreKey("appl_AbCdEf123"), Is.False);
            Assert.That(PurchasesConfig.IsTestStoreKey(null), Is.False);
        }

        // The Test Store project is shared with the app template, whose paywall is its current offering
        [Test]
        public void The_shop_sells_the_shop_offering()
        {
            Assert.That(PurchasesConfig.ShopOffering, Is.EqualTo("shop"));
        }
    }
}
