using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Core.Purchases.Tests;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Features.Showcase.Common.Purchases.Tests
{
    public sealed class ShowcaseShopPresenterTests
    {
        private FakeShopButtonView view;
        private FakePurchases purchases;
        private FakeShop shop;
        private FakeUiFeedback feedback;

        [SetUp]
        public void SetUp()
        {
            view = new FakeShopButtonView();
            purchases = new FakePurchases();
            shop = new FakeShop();
            feedback = new FakeUiFeedback();
        }

        private ShowcaseShopPresenter Create()
        {
            var presenter = new ShowcaseShopPresenter(view, purchases, shop, feedback);
            presenter.Start();
            return presenter;
        }

        [Test]
        public void Hidden_while_the_shop_cannot_show()
        {
            shop.CanShow = false;
            Create();

            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void Shown_while_the_shop_can_show()
        {
            Create();

            Assert.That(view.Visible, Is.True);
        }

        [Test]
        public void Hidden_once_nothing_is_left_to_sell()
        {
            Create();

            shop.CanShow = false;
            purchases.RaiseChanged();
            Assert.That(view.Visible, Is.False);

            shop.CanShow = true;
            purchases.RaiseTransactionsUpdated(new StoreTransaction[0]);
            Assert.That(view.Visible, Is.True);
        }

        [Test]
        public void A_tap_opens_the_shop_from_the_menu()
        {
            Create();

            view.Click();

            Assert.That(shop.Shown, Is.EqualTo(new[] { ShopSource.Menu }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void After_dispose_nothing_reaches_the_view_or_the_shop()
        {
            var presenter = Create();
            presenter.Dispose();

            shop.CanShow = false;
            purchases.RaiseChanged();
            view.Click();

            Assert.That(view.Visible, Is.True);
            Assert.That(shop.Shown, Is.Empty);
        }
    }
}
