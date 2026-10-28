using System;
using System.Collections.Generic;
using System.Globalization;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Purchases;
using Kturker.UnityBase.Features.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // SceneBuilder builds the prefab (Resources/ShellPurchases/Shop): a full-screen panel with the wallet and Close on a
    // top bar, the title, a scrolling column (Rows pack rows: title, content line, price button, a discount pill in the
    // corner; the coin packs' grid; a status line with Try again; a message line) and Restore purchases and Close at the bottom (the top right stays clear for the debug NET button).
    // Hidden rows and packs leave no gap; the packs share the grid's width (ShopPackGrid); the column
    // scrolls when the full shop is taller than the screen.
    public sealed class ShopView : MonoBehaviour, IShopView
    {
        // Slots for every row a game's shop may have (EconomyConfig.MaxShopRows): the coin packs and the other rows
        public const int Packs = 6;
        public const int Rows = EconomyConfig.MaxShopRows - Packs;

        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject wallet;
        [SerializeField] private FallbackIcon walletIcon;
        [SerializeField] private TMP_Text walletCount;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private GameObject[] rows;
        [SerializeField] private LocalizedText[] rowTitles;
        [SerializeField] private LocalizedText[] rowContents;
        [SerializeField] private Button[] rowBuys;
        [SerializeField] private LocalizedText[] rowPrices;
        [SerializeField] private GameObject[] rowTagBoxes;
        [SerializeField] private LocalizedText[] rowTags;
        [SerializeField] private ShopPackGrid packGrid;
        [SerializeField] private GameObject[] packs;
        [SerializeField] private FallbackIcon[] packIcons;
        [SerializeField] private LocalizedText[] packAmounts;
        [SerializeField] private Button[] packBuys;
        [SerializeField] private LocalizedText[] packPrices;
        [SerializeField] private GameObject[] packTagBoxes;
        [SerializeField] private LocalizedText[] packTags;
        [SerializeField] private LocalizedText status;
        [SerializeField] private Button retryButton;
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button restoreButton;

        private readonly string[] rowIds = new string[Rows];
        private readonly string[] packIds = new string[Packs];
        private CurrencyMark mark;

        public event Action CloseClicked;
        public event Action RestoreClicked;
        public event Action RetryClicked;
        public event Action<string> BuyClicked;

        public int MaxRows => rows.Length;
        public int MaxPacks => packs.Length;

        private void Awake()
        {
            KitScreens.Hide(panel);
            closeButton.onClick.AddListener(OnClose);
            restoreButton.onClick.AddListener(OnRestore);
            retryButton.onClick.AddListener(OnRetry);
            for (var i = 0; i < rowBuys.Length; i++)
            {
                var index = i;
                rowBuys[i].onClick.AddListener(() => Buy(rowIds[index]));
            }
            for (var i = 0; i < packBuys.Length; i++)
            {
                var index = i;
                packBuys[i].onClick.AddListener(() => Buy(packIds[index]));
            }
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnClose);
            restoreButton.onClick.RemoveListener(OnRestore);
            retryButton.onClick.RemoveListener(OnRetry);
            foreach (var button in rowBuys) button.onClick.RemoveAllListeners();
            foreach (var button in packBuys) button.onClick.RemoveAllListeners();
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowWallet(int? coins, CurrencyMark currencyMark)
        {
            mark = currencyMark;
            wallet.SetActive(coins.HasValue);
            if (!coins.HasValue) return;
            walletCount.text = coins.Value.ToString(CultureInfo.InvariantCulture);
            walletIcon.ShowCurrency(mark);
        }

        public void ShowLoading()
        {
            ShowStatus("common.loading", false);
            scroll.verticalNormalizedPosition = 1f;
        }

        public void ShowRows(IReadOnlyList<ShopRow> list, IReadOnlyList<ShopRow> packList, int packColumns)
        {
            status.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            for (var i = 0; i < rows.Length; i++)
            {
                var visible = i < list.Count;
                rows[i].SetActive(visible);
                rowIds[i] = visible ? list[i].Id : null;
                if (!visible) continue;
                var row = list[i];
                rowTitles[i].SetKey(row.TitleKey);
                rowContents[i].gameObject.SetActive(!string.IsNullOrEmpty(row.Content));
                rowContents[i].SetLiteral(row.Content ?? string.Empty);
                rowPrices[i].SetLiteral(row.Price);
                ShowTag(rowTagBoxes[i], rowTags[i], row.Tag);
            }
            for (var i = 0; i < packs.Length; i++)
            {
                var visible = i < packList.Count;
                packs[i].SetActive(visible);
                packIds[i] = visible ? packList[i].Id : null;
                if (!visible) continue;
                var pack = packList[i];
                packIcons[i].ShowCurrency(mark);
                packAmounts[i].SetLiteral(pack.Content);
                packPrices[i].SetLiteral(pack.Price);
                ShowTag(packTagBoxes[i], packTags[i], pack.Tag);
            }
            packGrid.gameObject.SetActive(packList.Count > 0);
            packGrid.constraintCount = Mathf.Max(1, packColumns);
        }

        public void ShowFailed() => ShowStatus(PurchasesKeys.Unavailable, true);

        public void ShowMessage(string key)
        {
            message.gameObject.SetActive(key != null);
            if (key != null) message.SetKey(key);
        }

        public void SetBusy(bool busy)
        {
            foreach (var button in rowBuys) button.interactable = !busy;
            foreach (var button in packBuys) button.interactable = !busy;
            restoreButton.interactable = !busy;
            closeButton.interactable = !busy;
            retryButton.interactable = !busy;
        }

        private static void ShowTag(GameObject box, LocalizedText text, int? tag)
        {
            box.SetActive(tag.HasValue);
            if (tag.HasValue) text.SetKey(PurchasesKeys.Tag, tag.Value);
        }

        private void ShowStatus(string key, bool retry)
        {
            foreach (var row in rows) row.SetActive(false);
            foreach (var pack in packs) pack.SetActive(false);
            packGrid.gameObject.SetActive(false);
            status.gameObject.SetActive(true);
            status.SetKey(key);
            retryButton.gameObject.SetActive(retry);
        }

        private void Buy(string id)
        {
            if (id != null) BuyClicked?.Invoke(id);
        }

        private void OnClose() => CloseClicked?.Invoke();

        private void OnRestore() => RestoreClicked?.Invoke();

        private void OnRetry() => RetryClicked?.Invoke();
    }
}
