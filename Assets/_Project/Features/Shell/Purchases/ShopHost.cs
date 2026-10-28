using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Economy;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // IShop for the root scope: makes the shop from Resources the first time it is shown and keeps it across scenes
    // (canvas sorting 800: above Settings and the economy windows, below the network inspector and the loading overlay).
    // It shows only while CanShow: purchases available and a row to sell (ShopPresenter.Lineup).
    public sealed class ShopHost : IShop, IDisposable
    {
        public const string ResourcePath = "ShellPurchases/Shop";

        private readonly IPurchases purchases;
        private readonly IShopCatalog catalog;
        private readonly IEconomy economy;
        private readonly EconomyCurrency currency;
        private readonly ILocalizer localizer;
        private readonly bool adsOn;
        private readonly IUiFeedback feedback;
        private readonly UiBinder binder;
        private readonly ILog log;
        private readonly Func<bool> autoPlay;
        private ShopView view;
        private ShopPresenter presenter;

        // catalog null: Remove Ads only; adsOn false: a game without ads (no Remove Ads row)
        public ShopHost(IPurchases purchases, IShopCatalog catalog, IEconomy economy, EconomyCurrency currency, ILocalizer localizer,
            bool adsOn, IUiFeedback feedback, UiBinder binder, ILog log, Func<bool> autoPlay)
        {
            this.purchases = purchases;
            this.catalog = catalog;
            this.economy = economy;
            this.currency = currency;
            this.localizer = localizer;
            this.adsOn = adsOn;
            this.feedback = feedback;
            this.binder = binder;
            this.log = log;
            this.autoPlay = autoPlay;
        }

        public bool CanShow => ShopPresenter.CanShow(catalog, purchases, adsOn);

        public void Show(ShopSource source)
        {
            if (!CanShow || autoPlay()) return;
            if (presenter == null && !Create()) return;
            presenter.Open(source);
        }

        public void Dispose()
        {
            if (presenter != null) presenter.Dispose();
            if (view != null) UnityEngine.Object.Destroy(view.gameObject);
        }

        private bool Create()
        {
            var prefab = Resources.Load<ShopView>(ResourcePath);
            if (prefab == null)
            {
                log.Error($"Shop missing: Resources/{ResourcePath}");
                return false;
            }
            view = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(view.gameObject);
            binder.BindTree(view.gameObject);
            presenter = new ShopPresenter(view, purchases, catalog, economy, currency, localizer, adsOn, feedback, log, autoPlay);
            presenter.Start();
            return true;
        }
    }
}
