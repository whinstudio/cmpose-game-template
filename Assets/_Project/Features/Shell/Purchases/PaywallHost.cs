using System;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // IPaywall for the root scope: makes the overlay from Resources the first time it is shown and keeps it across
    // scenes (canvas sorting 800: above Settings, below the network inspector and the loading overlay)
    public sealed class PaywallHost : IPaywall, IDisposable
    {
        public const string ResourcePath = "ShellPurchases/Paywall";

        private readonly IPurchases purchases;
        private readonly AboutInfo about;
        private readonly IUrlOpener urls;
        private readonly IUiFeedback feedback;
        private readonly UiBinder binder;
        private readonly ILog log;
        private readonly IPaywallBonus bonus;
        private readonly ILocalizer localizer;
        private PaywallView view;
        private PaywallPresenter presenter;

        // bonus may be null (no economy)
        public PaywallHost(IPurchases purchases, AboutInfo about, IUrlOpener urls, IUiFeedback feedback, UiBinder binder, ILog log,
            IPaywallBonus bonus = null, ILocalizer localizer = null)
        {
            this.bonus = bonus;
            this.localizer = localizer;
            this.purchases = purchases;
            this.about = about;
            this.urls = urls;
            this.feedback = feedback;
            this.binder = binder;
            this.log = log;
        }

        public void Show(PaywallSource source)
        {
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
            var prefab = Resources.Load<PaywallView>(ResourcePath);
            if (prefab == null)
            {
                log.Error($"Paywall missing: Resources/{ResourcePath}");
                return false;
            }
            view = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(view.gameObject);
            binder.BindTree(view.gameObject);
            presenter = new PaywallPresenter(view, purchases, about, urls, feedback, log, bonus, localizer);
            presenter.Start();
            return true;
        }
    }
}
