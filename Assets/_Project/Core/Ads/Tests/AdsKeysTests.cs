using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    public sealed class AdsKeysTests
    {
        [Test]
        public void Every_key_has_an_english_text()
        {
            var english = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            var keys = new[] { AdsKeys.Bonus, AdsKeys.BonusHint, AdsKeys.NotAvailable, AdsKeys.Unavailable };
            foreach (var key in keys) Assert.That(english.Get(key), Is.Not.EqualTo(key), key);
        }
    }
}
