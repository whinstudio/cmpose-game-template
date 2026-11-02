using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Purchases
{
    // The settings card's primary button, Remove Ads: shown while it can be bought (purchases available, not premium);
    // a press opens the shop
    public sealed class RemoveAdsAction : ISettingsAction
    {
        private readonly IPurchases purchases;
        private readonly IShop shop;

        public RemoveAdsAction(IPurchases purchases, IShop shop)
        {
            this.purchases = purchases;
            this.shop = shop;
            purchases.Changed += OnChanged;
        }

        public SettingsButton Button => SettingsButton.RemoveAds;
        public bool Visible => purchases.IsAvailable && !purchases.IsPremium;

        public event Action Changed;

        public UniTask<string> PressAsync(CancellationToken ct)
        {
            if (Visible) shop.Show(ShopSource.Settings);
            return UniTask.FromResult<string>(null);
        }

        private void OnChanged() => Changed?.Invoke();
    }
}
