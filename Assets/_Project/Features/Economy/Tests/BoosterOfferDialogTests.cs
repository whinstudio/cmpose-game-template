using System.Threading;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Economy.Tests
{
    // The booster offer's "not enough" moment: a Shop button opens the shop over the offer, which stays open
    public sealed class BoosterOfferDialogTests
    {
        private const string Json =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}]}";

        private FakeBoosterOfferView view;
        private FakeEconomyAds ads;
        private FakeEconomyShop shop;
        private FakeUiFeedback feedback;
        private Economy economy;
        private BoosterOfferDialog dialog;

        [SetUp]
        public void SetUp()
        {
            view = new FakeBoosterOfferView();
            ads = new FakeEconomyAds { RewardedAvailable = false };
            shop = new FakeEconomyShop();
            feedback = new FakeUiFeedback();
            economy = new Economy(EconomyConfig.Parse(Json), new MemoryEconomyStore());
            dialog = new BoosterOfferDialog(view, economy, ads, new EconomyCurrency(economy.Config, new FakeLocalizer()), feedback, shop,
                () => false);
        }

        [TearDown]
        public void TearDown() => dialog.Dispose();

        private void Offer() => _ = dialog.OfferAsync(economy.Config.Booster("bomb"), true, CancellationToken.None);

        [Test]
        public void not_enough_shows_a_shop_button_that_opens_the_shop_with_NotEnough()
        {
            economy.Earn(20);
            Offer();

            Assert.That(view.Message, Is.EqualTo(EconomyKeys.CurrencyNotEnough));
            Assert.That(view.ShopShown, Is.True);
            view.ClickShop();

            Assert.That(shop.Shown, Is.EqualTo(new[] { true }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
            Assert.That(view.Open, Is.True, "the offer waits under the shop");
        }

        [Test]
        public void a_pack_bought_in_the_shop_enables_the_price_and_hides_the_button()
        {
            Offer();
            view.ClickShop();

            economy.Earn(300);

            Assert.That(view.CanBuy, Is.True);
            Assert.That(view.ShopShown, Is.False);
            Assert.That(view.Message, Is.Null);
        }

        [Test]
        public void enough_coins_show_no_shop_button()
        {
            economy.Earn(100);
            Offer();

            Assert.That(view.ShopShown, Is.False);
        }

        [Test]
        public void a_shop_that_cannot_show_has_no_button_and_a_press_does_nothing()
        {
            shop.CanShow = false;
            Offer();

            Assert.That(view.ShopShown, Is.False);
            view.ClickShop();
            Assert.That(shop.Shown, Is.Empty);
        }

        [Test]
        public void a_closed_offer_ignores_the_shop_button()
        {
            view.ClickShop();

            Assert.That(shop.Shown, Is.Empty);
        }
    }
}
