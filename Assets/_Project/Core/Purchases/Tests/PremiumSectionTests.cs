using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class PremiumSectionTests
    {
        [Test]
        public void The_row_shows_get_active_or_unavailable()
        {
            var purchases = new FakePurchases();
            var section = new PremiumSection(purchases, new FakeShop());

            Assert.That(section.TitleKey, Is.EqualTo(PurchasesKeys.SettingsTitle));
            Assert.That(section.Options.Count, Is.EqualTo(1));
            Assert.That(section.Selected, Is.Zero);
            Assert.That(section.Options[0].LabelKey, Is.EqualTo(PurchasesKeys.StateGet));
            purchases.IsPremium = true;
            Assert.That(section.Options[0].LabelKey, Is.EqualTo(PurchasesKeys.StateActive));
            purchases.IsPremium = false;
            purchases.IsAvailable = false;
            Assert.That(section.Options[0].LabelKey, Is.EqualTo(PurchasesKeys.StateUnavailable));
        }

        [Test]
        public void Get_opens_the_shop_from_settings()
        {
            var purchases = new FakePurchases();
            var shop = new FakeShop();
            var section = new PremiumSection(purchases, shop);

            section.Select(0);
            purchases.IsAvailable = false;
            section.Select(0);
            purchases.IsAvailable = true;
            section.Select(1);

            Assert.That(shop.Shown, Is.EqualTo(new[] { ShopSource.Settings }));
        }

        [Test]
        public void Active_when_premium()
        {
            var purchases = new FakePurchases { IsPremium = true };
            var shop = new FakeShop();
            var section = new PremiumSection(purchases, shop);

            section.Select(0);

            Assert.That(section.Options[0].LabelKey, Is.EqualTo(PurchasesKeys.StateActive));
            Assert.That(shop.Shown, Is.Empty, "Remove Ads is already on: nothing to sell");
        }

        [Test]
        public void Changed_follows_the_purchases()
        {
            var purchases = new FakePurchases();
            var section = new PremiumSection(purchases, new FakeShop());
            var changes = 0;
            section.Changed += () => changes++;

            purchases.RaiseChanged();

            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
