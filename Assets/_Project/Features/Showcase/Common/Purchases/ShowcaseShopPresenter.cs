using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;

namespace Kturker.UnityBase.Features.Showcase.Common.Purchases
{
    // The purchases flag in the showcase: a Shop row in the menu opens the shop. It shows while the shop can show:
    // purchases available (not the editor, nor a Test Store key in release) and a row left to sell (IShop.CanShow).
    public sealed class ShowcaseShopPresenter : Presenter<IShopButtonView>
    {
        private readonly IPurchases purchases;
        private readonly IShop shop;
        private readonly IUiFeedback feedback;

        public ShowcaseShopPresenter(IShopButtonView view, IPurchases purchases, IShop shop, IUiFeedback feedback)
            : base(view)
        {
            this.purchases = purchases;
            this.shop = shop;
            this.feedback = feedback;
        }

        public override void Start()
        {
            purchases.Changed += Refresh;
            purchases.TransactionsUpdated += OnTransactions;
            View.Clicked += OnClicked;
            Refresh();
        }

        public override void Dispose()
        {
            purchases.Changed -= Refresh;
            purchases.TransactionsUpdated -= OnTransactions;
            View.Clicked -= OnClicked;
        }

        private void Refresh() => View.SetVisible(shop.CanShow);

        private void OnTransactions(IReadOnlyList<StoreTransaction> transactions) => Refresh();

        private void OnClicked()
        {
            feedback.Click();
            shop.Show(ShopSource.Menu);
        }
    }
}
