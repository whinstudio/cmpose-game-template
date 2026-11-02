using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    public sealed class PurchasesActionsTests
    {
        [Test]
        public void Remove_ads_is_visible_only_while_it_can_be_bought()
        {
            var purchases = new FakePurchases();
            var shop = new FakeShop();
            var action = new RemoveAdsAction(purchases, shop);
            var changes = 0;
            action.Changed += () => changes++;

            Assert.That(action.Button, Is.EqualTo(SettingsButton.RemoveAds));
            Assert.That(action.Visible, Is.True);
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(shop.Shown, Is.EqualTo(new[] { ShopSource.Settings }));

            purchases.IsPremium = true;
            purchases.RaiseChanged();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(action.Visible, Is.False, "already bought");
            action.PressAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(shop.Shown.Count, Is.EqualTo(1), "nothing to sell");

            purchases.IsPremium = false;
            purchases.IsAvailable = false;
            Assert.That(action.Visible, Is.False, "purchases unavailable");
        }

        [Test]
        public void Restore_reports_restored_or_the_error_key()
        {
            var purchases = new FakePurchases();
            var action = new RestoreAction(purchases);
            var changes = 0;
            action.Changed += () => changes++;

            Assert.That(action.Button, Is.EqualTo(SettingsButton.Restore));
            Assert.That(action.Visible, Is.True);
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(PurchasesKeys.Restored));

            purchases.RestoreResult = PurchaseResult.Error(PurchasesKeys.ErrorNothingToRestore);
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(),
                Is.EqualTo(PurchasesKeys.ErrorNothingToRestore));

            purchases.RestoreResult = PurchaseResult.Cancelled;
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.Null, "backed out");
            Assert.That(purchases.Restores, Is.EqualTo(3));

            purchases.IsAvailable = false;
            purchases.RaiseChanged();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(action.Visible, Is.False);
            Assert.That(action.PressAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(purchases.Restores, Is.EqualTo(3), "nothing reaches the store while unavailable");
        }

        [Test]
        public void Restore_answers_once_the_store_does()
        {
            var purchases = new FakePurchases { PendingPurchase = new UniTaskCompletionSource<PurchaseResult>() };
            var action = new RestoreAction(purchases);
            var answer = "none";

            async UniTaskVoid Press() => answer = await action.PressAsync(CancellationToken.None);
            Press().Forget();
            Assert.That(answer, Is.EqualTo("none"));

            purchases.PendingPurchase.TrySetResult(PurchaseResult.Success);
            Assert.That(answer, Is.EqualTo(PurchasesKeys.Restored));
        }
    }
}
