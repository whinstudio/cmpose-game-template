using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class PurchasesKeysTests
    {
        [Test]
        public void Every_key_has_an_english_text()
        {
            var english = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            var keys = new[]
            {
                PurchasesKeys.SettingsTitle, PurchasesKeys.StateGet, PurchasesKeys.StateActive, PurchasesKeys.StateUnavailable,
                PurchasesKeys.Title, PurchasesKeys.Restore, PurchasesKeys.Restored, PurchasesKeys.Retry, PurchasesKeys.NoAds,
                PurchasesKeys.Tag, PurchasesKeys.Unavailable, PurchasesKeys.ErrorUnavailable, PurchasesKeys.ErrorPackageUnavailable,
                PurchasesKeys.ErrorNothingToRestore
            };
            foreach (var key in keys) Assert.That(english.Get(key), Is.Not.EqualTo(key), key);
            foreach (var id in new[] { ShopProducts.RemoveAds, ShopProducts.StarterPack, ShopProducts.NoAdsBundle, "bundle_1", "bundle_2", "bundle_3" })
                Assert.That(english.Get(PurchasesKeys.RowTitle(id)), Is.Not.EqualTo(PurchasesKeys.RowTitle(id)), id);
            foreach (StoreErrorKind kind in Enum.GetValues(typeof(StoreErrorKind)))
                Assert.That(english.Get(PurchasesKeys.Error(kind)), Is.Not.EqualTo(PurchasesKeys.Error(kind)), kind.ToString());
        }
    }
}
