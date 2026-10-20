using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Features.Shell.Purchases;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class FakePaywallView : IPaywallView
    {
        public readonly List<string> Calls = new List<string>();
        public IReadOnlyList<PaywallRow> Rows = new PaywallRow[0];
        public int Selected = -1;
        public bool Open;
        public bool Busy;
        public string Message;
        public string Bonus;
        public bool Privacy;
        public bool Terms;
        public event Action CloseClicked;
        public event Action BuyClicked;
        public event Action RestoreClicked;
        public event Action PrivacyClicked;
        public event Action TermsClicked;
        public event Action<int> PackageClicked;
        public int MaxPackages => 4;
        public void SetOpen(bool open) { Open = open; Calls.Add(open ? "open" : "closed"); }
        public void SetLegal(bool privacy, bool terms) { Privacy = privacy; Terms = terms; }
        public void ShowLoading() => Calls.Add("loading");
        public void ShowPackages(IReadOnlyList<PaywallRow> rows, int selected) { Rows = rows; Selected = selected; Calls.Add($"packages {rows.Count}"); }
        public void Select(int index) => Selected = index;
        public void ShowUnavailable() => Calls.Add("unavailable");
        public void ShowMessage(string key) { Message = key; Calls.Add($"message {key}"); }
        public void SetBusy(bool busy) => Busy = busy;
        public void ShowBonus(string text) => Bonus = text;
        public void ClickClose() => CloseClicked?.Invoke();
        public void ClickBuy() => BuyClicked?.Invoke();
        public void ClickRestore() => RestoreClicked?.Invoke();
        public void ClickPrivacy() => PrivacyClicked?.Invoke();
        public void ClickTerms() => TermsClicked?.Invoke();
        public void ClickPackage(int index) => PackageClicked?.Invoke(index);
    }

    public sealed class PaywallPresenterTests
    {
        private static readonly PaywallOffer Offer = new PaywallOffer(new[]
        {
            new PaywallPackage("$rc_monthly", PackagePeriod.Monthly, "$9.99", null, null, null),
            new PaywallPackage("$rc_annual", PackagePeriod.Annual, "$59.99", "$4.99", 50, null),
            new PaywallPackage("$rc_weekly", PackagePeriod.Weekly, "$2.99", "$12.99", null, new TrialPeriod(3, TrialUnit.Day)),
            new PaywallPackage("$rc_lifetime", PackagePeriod.Lifetime, "$99.99", null, null, null),
            new PaywallPackage("custom", PackagePeriod.Other, "$1.99", null, null, null)
        });

        private static PaywallPresenter Make(out FakePaywallView view, out FakePurchases purchases, out FakeLog log,
            out FakeUrlOpener urls, string privacy = "https://example.test/privacy", string terms = "")
        {
            view = new FakePaywallView();
            purchases = new FakePurchases { Offer = Offer };
            log = new FakeLog();
            urls = new FakeUrlOpener();
            var presenter = new PaywallPresenter(view, purchases, new AboutInfo("1.0.0", privacy, terms, ""), urls, new FakeUiFeedback(), log);
            presenter.Start();
            return presenter;
        }

        [Test]
        public void Open_loads_then_shows_at_most_four_rows_with_annual_selected()
        {
            var presenter = Make(out var view, out _, out var log, out _);

            presenter.Open(PaywallSource.First);

            Assert.That(view.Calls, Is.EqualTo(new[] { "open", "loading", "packages 4" }));
            Assert.That(view.Selected, Is.EqualTo(1));
            Assert.That(view.Rows.Select(r => r.PeriodKey), Is.EqualTo(new[]
            {
                "purchases.period.monthly", "purchases.period.annual", "purchases.period.weekly", "purchases.period.lifetime"
            }));
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PAYWALL shown=first packages=4"));
            Assert.That(presenter.IsOpen, Is.True);
        }

        [Test]
        public void Opening_from_the_menu_marks_the_menu_as_the_source()
        {
            var presenter = Make(out _, out _, out var log, out _);

            presenter.Open(PaywallSource.Menu);

            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PAYWALL shown=menu packages=4"));
        }

        [Test]
        public void Rows_carry_the_trial_or_the_monthly_price_and_the_savings()
        {
            var annual = PaywallPresenter.Row(Offer.Packages[1]);
            Assert.That(annual.Price, Is.EqualTo("$59.99"));
            Assert.That(annual.DetailKey, Is.EqualTo(PurchasesKeys.PerMonth));
            Assert.That(annual.DetailArg, Is.EqualTo("$4.99"));
            Assert.That(annual.SavingsPercent, Is.EqualTo(50));

            var weekly = PaywallPresenter.Row(Offer.Packages[2]);
            Assert.That(weekly.DetailKey, Is.EqualTo("purchases.trial.day.other"));
            Assert.That(weekly.DetailArg, Is.EqualTo("3"));

            var monthly = PaywallPresenter.Row(Offer.Packages[0]);
            Assert.That(monthly.DetailKey, Is.Null);
            Assert.That(monthly.SavingsPercent, Is.Null);
        }

        [Test]
        public void No_offer_shows_unavailable_and_without_annual_the_first_is_selected()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            purchases.Offer = null;
            presenter.Open(PaywallSource.Settings);
            Assert.That(view.Calls.Last(), Is.EqualTo("unavailable"));
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PAYWALL shown=settings packages=0"));

            var other = Make(out var otherView, out var otherPurchases, out _, out _);
            otherPurchases.Offer = new PaywallOffer(new[] { Offer.Packages[0], Offer.Packages[3] });
            other.Open(PaywallSource.Settings);
            Assert.That(otherView.Selected, Is.Zero);
        }

        [Test]
        public void A_successful_purchase_buys_the_selected_package_and_closes()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            presenter.Open(PaywallSource.First);

            view.ClickPackage(3);
            view.ClickBuy();

            Assert.That(purchases.Purchased, Is.EqualTo(new[] { "$rc_lifetime" }));
            Assert.That(view.Open, Is.False);
            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_PURCHASE result=Success"));
        }

        [Test]
        public void Cancelled_stays_silent_and_an_error_shows_its_text()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            presenter.Open(PaywallSource.First);

            purchases.PurchaseResult = PurchaseResult.Cancelled;
            view.ClickBuy();
            Assert.That(view.Message, Is.Null);
            Assert.That(view.Open, Is.True);

            purchases.PurchaseResult = PurchaseResult.Error("purchases.error.network");
            view.ClickBuy();
            Assert.That(view.Message, Is.EqualTo("purchases.error.network"));
            Assert.That(view.Open, Is.True);
            Assert.That(purchases.Purchased, Is.EqualTo(new[] { "$rc_annual", "$rc_annual" }));
            Assert.That(log.Lines.Where(l => l.StartsWith("MARKER CMPOSE_PURCHASE ")), Is.EqualTo(new[]
            {
                "MARKER CMPOSE_PURCHASE result=Cancelled", "MARKER CMPOSE_PURCHASE result=Error"
            }));
        }

        [Test]
        public void Presses_while_busy_are_ignored()
        {
            var presenter = Make(out var view, out var purchases, out _, out _);
            presenter.Open(PaywallSource.First);
            purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();

            view.ClickBuy();
            Assert.That(view.Busy, Is.True);
            view.ClickBuy();
            view.ClickRestore();
            view.ClickPackage(0);
            view.ClickClose();
            Assert.That(purchases.Purchased.Count, Is.EqualTo(1));
            Assert.That(purchases.Restores, Is.Zero);
            Assert.That(view.Selected, Is.EqualTo(1));
            Assert.That(view.Open, Is.True);

            purchases.PendingPurchase.TrySetResult(PurchaseResult.Success);
            Assert.That(view.Busy, Is.False);
            Assert.That(view.Open, Is.False);
        }

        [Test]
        public void Restore_errors_show_their_text_and_success_closes()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            presenter.Open(PaywallSource.Settings);

            purchases.RestoreResult = PurchaseResult.Error(PurchasesKeys.ErrorNothingToRestore);
            view.ClickRestore();
            Assert.That(view.Message, Is.EqualTo(PurchasesKeys.ErrorNothingToRestore));

            purchases.RestoreResult = PurchaseResult.Success;
            view.ClickRestore();
            Assert.That(view.Open, Is.False);
            Assert.That(log.Lines, Has.Some.EqualTo("MARKER CMPOSE_RESTORE result=Success"));
        }

        [Test]
        public void A_late_offer_after_close_changes_nothing_and_reopening_loads_again()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            purchases.PendingOffer = new UniTaskCompletionSource<PaywallOffer>();
            presenter.Open(PaywallSource.First);
            var token = purchases.LastOfferToken;

            view.ClickClose();
            Assert.That(token.IsCancellationRequested, Is.True);
            purchases.PendingOffer.TrySetResult(Offer);

            Assert.That(view.Calls, Is.EqualTo(new[] { "open", "loading", "closed" }));
            Assert.That(log.Lines.Any(l => l.Contains("CMPOSE_PAYWALL")), Is.False);

            purchases.PendingOffer = null;
            presenter.Open(PaywallSource.Settings);
            Assert.That(purchases.OfferCalls, Is.EqualTo(2));
            Assert.That(view.Calls.Last(), Is.EqualTo("packages 4"));
        }

        [Test]
        public void Opening_an_open_paywall_does_nothing_and_legal_links_follow_about()
        {
            var presenter = Make(out var view, out var purchases, out _, out var urls);
            presenter.Open(PaywallSource.First);
            presenter.Open(PaywallSource.Settings);

            Assert.That(purchases.OfferCalls, Is.EqualTo(1));
            Assert.That(view.Privacy, Is.True);
            Assert.That(view.Terms, Is.False);
            view.ClickPrivacy();
            Assert.That(urls.Opened, Is.EqualTo(new[] { "https://example.test/privacy" }));
        }

        [Test]
        public void Dispose_cancels_and_a_late_result_changes_nothing()
        {
            var presenter = Make(out var view, out var purchases, out var log, out _);
            presenter.Open(PaywallSource.First);
            purchases.PendingPurchase = new UniTaskCompletionSource<PurchaseResult>();
            view.ClickBuy();

            presenter.Dispose();
            purchases.PendingPurchase.TrySetResult(PurchaseResult.Error("purchases.error.failed"));
            view.ClickBuy();

            Assert.That(view.Message, Is.Null);
            Assert.That(purchases.Purchased.Count, Is.EqualTo(1));
            Assert.That(log.Lines.Any(l => l.Contains("CMPOSE_PURCHASE ")), Is.False);
        }

        // A game economy whose welcome bundle is 600 coins, 3 bombs and 3 shields
        private const string EconomyJson =
            "{\"version\":1,\"currency\":{\"name\":\"coins\",\"color\":\"#F9B817\"}," +
            "\"boosters\":[{\"id\":\"bomb\",\"price\":80,\"offer\":\"inRun\",\"start\":0}," +
            "{\"id\":\"shield\",\"price\":120,\"offer\":\"preRun\",\"start\":0}]," +
            "\"supply\":{\"freeCoins\":15,\"freeHours\":3,\"adCoins\":[20]}," +
            "\"wheel\":{\"freeEvery\":4,\"adSpinsPerDay\":3,\"slices\":[{\"coins\":15,\"weight\":1}]}," +
            "\"login\":[{\"coins\":15},{\"coins\":15},{\"coins\":30},{\"coins\":30},{\"coins\":45},{\"coins\":45},{\"coins\":200}]," +
            "\"extras\":[{\"id\":\"supply\",\"kind\":\"supply\"},{\"id\":\"wheel\",\"kind\":\"wheel\"},{\"id\":\"login\",\"kind\":\"login\"}]," +
            "\"welcome\":{\"coins\":600,\"boosters\":{\"bomb\":3,\"shield\":3}}}";

        [Test]
        public void Paywall_shows_the_welcome_bundle_when_bound()
        {
            var view = new FakePaywallView();
            var purchases = new FakePurchases { Offer = Offer };
            var economy = new Economy(EconomyConfig.Parse(EconomyJson), new MemoryEconomyStore());
            var bundle = new WelcomeBundle(economy, new PurchasesPremiumEvents(purchases));
            bundle.Initialize();
            var presenter = new PaywallPresenter(view, purchases, new AboutInfo("1.0.0", "", "", ""), new FakeUrlOpener(), new FakeUiFeedback(),
                new FakeLog(), new WelcomeBonusLine(economy, new FakeLocalizer()), new FakeLocalizer());
            presenter.Start();

            presenter.Open(PaywallSource.Menu);

            Assert.That(view.Bonus, Is.EqualTo("economy.welcome.line(+600 coins, +3 economy.booster.bomb.name, +3 economy.booster.shield.name)"));
            view.ClickBuy();
            purchases.RaisePremiumPurchased();
            Assert.That(economy.Coins, Is.EqualTo(600));
            presenter.Open(PaywallSource.Settings);
            Assert.That(view.Bonus, Is.Null, "given once: the line goes");
            bundle.Dispose();
        }

        [Test]
        public void Paywall_without_economy_is_unchanged()
        {
            var presenter = Make(out var view, out _, out _, out _);
            presenter.Open(PaywallSource.First);

            Assert.That(view.Calls, Is.EqualTo(new[] { "open", "loading", "packages 4" }));
            Assert.That(view.Bonus, Is.Null);

            // An economy without GameEconomy.json binds a line with nothing in it
            var offView = new FakePaywallView();
            var off = new PaywallPresenter(offView, new FakePurchases { Offer = Offer }, new AboutInfo("1.0.0", "", "", ""), new FakeUrlOpener(),
                new FakeUiFeedback(), new FakeLog(), new WelcomeBonusLine(new Economy(null, new MemoryEconomyStore()), new FakeLocalizer()), new FakeLocalizer());
            off.Start();
            off.Open(PaywallSource.First);
            Assert.That(offView.Calls, Is.EqualTo(new[] { "open", "loading", "packages 4" }));
            Assert.That(offView.Bonus, Is.Null);
        }
    }
}
