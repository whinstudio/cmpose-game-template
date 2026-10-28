using System;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows (Resources/Economy/EconomyWindows): a centred modal with the
    // booster's icon, name and effect, the currency the player has, the price with the currency's icon and "Watch ad"
    // side by side (one fills the row when the other hides), a message line, Shop (while the coins do not reach the
    // price) and Close
    public sealed class BoosterOfferView : MonoBehaviour, IBoosterOfferView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private FallbackIcon icon;
        [SerializeField] private LocalizedText title;
        [SerializeField] private LocalizedText description;
        [SerializeField] private LocalizedText have;
        [SerializeField] private Button buyButton;
        [SerializeField] private LocalizedText buyLabel;
        [SerializeField] private FallbackIcon buyIcon;
        [SerializeField] private Button watchButton;
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button closeButton;
        private bool canBuy;
        private bool busy;

        public event Action BuyClicked;
        public event Action WatchClicked;
        public event Action CloseClicked;
        public event Action ShopClicked;

        public Button BuyButton => buyButton;
        public Button WatchButton => watchButton;
        public Button ShopButton => shopButton;
        public LocalizedText Message => message;

        private void Awake()
        {
            KitScreens.Hide(panel);
            buyButton.onClick.AddListener(OnBuy);
            watchButton.onClick.AddListener(OnWatch);
            closeButton.onClick.AddListener(OnClose);
            shopButton.onClick.AddListener(OnShop);
        }

        private void OnDestroy()
        {
            buyButton.onClick.RemoveListener(OnBuy);
            watchButton.onClick.RemoveListener(OnWatch);
            closeButton.onClick.RemoveListener(OnClose);
            shopButton.onClick.RemoveListener(OnShop);
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowBooster(string nameKey, string descriptionKey, Sprite sprite)
        {
            title.SetKey(nameKey);
            description.SetKey(descriptionKey);
            icon.ShowKey(sprite, nameKey);
        }

        public void ShowPrice(int price, int coins, EconomyCurrency currency)
        {
            buyLabel.SetKey(EconomyKeys.Price, price);
            buyIcon.ShowCurrency(currency.Mark);
            have.SetKey(EconomyKeys.CurrencyHave, coins, currency.NameArg);
        }

        public void SetActions(bool buy, bool adShown)
        {
            canBuy = buy;
            watchButton.gameObject.SetActive(adShown);
            Interactable();
        }

        public void SetShop(bool shown) => shopButton.gameObject.SetActive(shown);

        public void ShowMessage(string key, params object[] args)
        {
            message.gameObject.SetActive(key != null);
            if (key != null) message.SetKey(key, args);
        }

        public void SetBusy(bool value)
        {
            busy = value;
            Interactable();
        }

        private void Interactable()
        {
            buyButton.interactable = canBuy && !busy;
            watchButton.interactable = !busy;
            closeButton.interactable = !busy;
            shopButton.interactable = !busy;
        }

        private void OnBuy() => BuyClicked?.Invoke();

        private void OnWatch() => WatchClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();

        private void OnShop() => ShopClicked?.Invoke();
    }
}
