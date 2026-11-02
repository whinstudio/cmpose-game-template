using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Purchases
{
    // The settings card's Restore button: shown while purchases are available; a press restores the purchases and
    // answers with the shop's texts (Restored, or the error), nothing when the player backed out
    public sealed class RestoreAction : ISettingsAction
    {
        private readonly IPurchases purchases;

        public RestoreAction(IPurchases purchases)
        {
            this.purchases = purchases;
            purchases.Changed += OnChanged;
        }

        public SettingsButton Button => SettingsButton.Restore;
        public bool Visible => purchases.IsAvailable;

        public event Action Changed;

        public async UniTask<string> PressAsync(CancellationToken ct)
        {
            if (!Visible) return null;
            var result = await purchases.RestoreAsync(ct);
            switch (result.Outcome)
            {
                case PurchaseOutcome.Success: return PurchasesKeys.Restored;
                case PurchaseOutcome.Error: return result.MessageKey;
                default: return null;
            }
        }

        private void OnChanged() => Changed?.Invoke();
    }
}
