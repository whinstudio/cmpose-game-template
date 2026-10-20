using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // The paywall's states: loading, packages (Annual selected), unavailable, busy while a purchase or restore
    // runs (every press is ignored then, Close too). Success closes; Cancelled says nothing; Error shows its text.
    // With a bonus bound (the economy's welcome bundle) its line shows under the packages.
    // Rewrite the view for your game; keep this flow.
    public sealed class PaywallPresenter : IDisposable
    {
        private readonly IPaywallView view;
        private readonly IPurchases purchases;
        private readonly AboutInfo about;
        private readonly IUrlOpener urls;
        private readonly IUiFeedback feedback;
        private readonly ILog log;
        private readonly IPaywallBonus bonus;
        private readonly ILocalizer localizer;
        private IReadOnlyList<PaywallPackage> packages = new PaywallPackage[0];
        private CancellationTokenSource loading;
        private int selected;
        private bool busy;
        private bool disposed;

        public PaywallPresenter(IPaywallView view, IPurchases purchases, AboutInfo about, IUrlOpener urls, IUiFeedback feedback, ILog log,
            IPaywallBonus bonus = null, ILocalizer localizer = null)
        {
            this.bonus = bonus;
            this.localizer = localizer;
            this.view = view;
            this.purchases = purchases;
            this.about = about;
            this.urls = urls;
            this.feedback = feedback;
            this.log = log;
        }

        public bool IsOpen { get; private set; }

        public void Start()
        {
            view.CloseClicked += OnClose;
            view.BuyClicked += OnBuy;
            view.RestoreClicked += OnRestore;
            view.PrivacyClicked += OnPrivacy;
            view.TermsClicked += OnTerms;
            view.PackageClicked += OnPackage;
        }

        public void Dispose()
        {
            disposed = true;
            CancelLoading();
            view.CloseClicked -= OnClose;
            view.BuyClicked -= OnBuy;
            view.RestoreClicked -= OnRestore;
            view.PrivacyClicked -= OnPrivacy;
            view.TermsClicked -= OnTerms;
            view.PackageClicked -= OnPackage;
        }

        public void Open(PaywallSource source)
        {
            if (IsOpen || disposed) return;
            IsOpen = true;
            view.SetOpen(true);
            view.SetLegal(about.HasPrivacy, about.HasTerms);
            LoadAsync(source).Forget();
        }

        public static PaywallRow Row(PaywallPackage package)
        {
            string detailKey = null;
            string detailArg = null;
            if (package.FreeTrial.HasValue)
            {
                detailKey = PurchasesKeys.Trial(package.FreeTrial.Value);
                detailArg = package.FreeTrial.Value.Value.ToString();
            }
            else if (package.PricePerMonth != null)
            {
                detailKey = PurchasesKeys.PerMonth;
                detailArg = package.PricePerMonth;
            }
            return new PaywallRow(PurchasesKeys.Period(package.Period), package.Price, detailKey, detailArg, package.SavingsPercent);
        }

        private async UniTaskVoid LoadAsync(PaywallSource source)
        {
            CancelLoading();
            loading = new CancellationTokenSource();
            var token = loading.Token;
            packages = new PaywallPackage[0];
            view.ShowLoading();
            PaywallOffer offer;
            try
            {
                offer = await purchases.OfferAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || disposed) return;
            packages = offer == null ? new PaywallPackage[0] : offer.Packages.Take(view.MaxPackages).ToList();
            log.Marker($"CMPOSE_PAYWALL shown={source.ToString().ToLowerInvariant()} packages={packages.Count}");
            if (packages.Count == 0)
            {
                view.ShowUnavailable();
                return;
            }
            var annual = packages.ToList().FindIndex(p => p.Period == PackagePeriod.Annual);
            selected = annual >= 0 ? annual : 0;
            view.ShowPackages(packages.Select(Row).ToList(), selected);
            view.ShowBonus(BonusText(bonus, localizer));
        }

        // "Bonus: +600 coins, +3 Bomb"; null without a bonus or with nothing in it
        public static string BonusText(IPaywallBonus bonus, ILocalizer localizer)
        {
            if (bonus == null || localizer == null || bonus.LineKey == null) return null;
            var parts = bonus.Args.Select(part => $"+{part.Value} {localizer.Get(part.Key)}").ToList();
            return parts.Count == 0 ? null : localizer.Get(bonus.LineKey, string.Join(", ", parts));
        }

        private void OnPackage(int index)
        {
            if (busy || index < 0 || index >= packages.Count) return;
            selected = index;
            view.Select(index);
        }

        private void OnBuy()
        {
            if (busy || packages.Count == 0) return;
            feedback.Click();
            RunAsync(purchase: true).Forget();
        }

        private void OnRestore()
        {
            if (busy) return;
            feedback.Click();
            RunAsync(purchase: false).Forget();
        }

        private async UniTaskVoid RunAsync(bool purchase)
        {
            busy = true;
            view.SetBusy(true);
            var result = purchase
                ? await purchases.PurchaseAsync(packages[selected].Id, CancellationToken.None)
                : await purchases.RestoreAsync(CancellationToken.None);
            busy = false;
            if (disposed) return;
            view.SetBusy(false);
            log.Marker($"CMPOSE_{(purchase ? "PURCHASE" : "RESTORE")} result={result.Outcome}");
            if (result.Outcome == PurchaseOutcome.Success) Close();
            else if (result.Outcome == PurchaseOutcome.Error) view.ShowMessage(result.MessageKey);
        }

        private void OnClose()
        {
            if (busy) return;
            feedback.Click();
            Close();
        }

        private void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            CancelLoading();
            view.SetOpen(false);
        }

        private void OnPrivacy() => urls.Open(about.PrivacyUrl);

        private void OnTerms() => urls.Open(about.TermsUrl);

        private void CancelLoading()
        {
            if (loading == null) return;
            loading.Cancel();
            loading.Dispose();
            loading = null;
        }
    }
}
