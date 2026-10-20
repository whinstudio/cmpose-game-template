using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows: a centred modal with the free row (the currency's icon and amount,
    // Claim or "Ready in 2:59:41"), the ad rows' header and AdRows rows (icon and amount, Watch with the video mark,
    // "Claimed" or "Locked"), a message line and Close
    public sealed class SupplyWindowView : MonoBehaviour, ISupplyWindowView
    {
        public const int AdRows = 4;

        [SerializeField] private GameObject panel;
        [SerializeField] private FallbackIcon[] coinIcons;
        [SerializeField] private LocalizedText freeAmount;
        [SerializeField] private Button freeButton;
        [SerializeField] private LocalizedText freeTimer;
        [SerializeField] private GameObject adHeader;
        [SerializeField] private GameObject[] adRows;
        [SerializeField] private LocalizedText[] adAmounts;
        [SerializeField] private Button[] adButtons;
        [SerializeField] private LocalizedText[] adStates;
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button closeButton;

        public event Action FreeClicked;
        public event Action<int> AdClicked;
        public event Action CloseClicked;

        public int MaxAdRows => adRows.Length;

        private void Awake()
        {
            KitScreens.Hide(panel);
            freeButton.onClick.AddListener(OnFree);
            closeButton.onClick.AddListener(OnClose);
            for (var i = 0; i < adButtons.Length; i++)
            {
                var index = i;
                adButtons[i].onClick.AddListener(() => AdClicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            freeButton.onClick.RemoveListener(OnFree);
            closeButton.onClick.RemoveListener(OnClose);
            foreach (var button in adButtons) button.onClick.RemoveAllListeners();
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowCurrency(CurrencyMark mark)
        {
            foreach (var icon in coinIcons) icon.ShowCurrency(mark);
        }

        public void ShowFree(int coins, bool ready, string timer)
        {
            freeAmount.SetKey(EconomyKeys.Amount, coins);
            freeButton.gameObject.SetActive(ready);
            freeTimer.gameObject.SetActive(!ready);
            if (!ready) freeTimer.SetKey(EconomyKeys.SupplyReadyIn, timer);
        }

        public void ShowAdRows(IReadOnlyList<SupplyRow> rows)
        {
            adHeader.SetActive(rows.Count > 0);
            for (var i = 0; i < adRows.Length; i++)
            {
                var visible = i < rows.Count;
                adRows[i].SetActive(visible);
                if (!visible) continue;
                var row = rows[i];
                adAmounts[i].SetKey(EconomyKeys.Amount, row.Coins);
                adButtons[i].gameObject.SetActive(row.State == SupplyRowState.Ready);
                adStates[i].gameObject.SetActive(row.State != SupplyRowState.Ready);
                if (row.State == SupplyRowState.Ready) continue;
                var claimed = row.State == SupplyRowState.Claimed;
                adStates[i].SetKey(claimed ? EconomyKeys.SupplyClaimed : EconomyKeys.SupplyLocked);
                adStates[i].GetComponent<ThemedGraphic>().SetRole(claimed ? ThemeRole.Success : ThemeRole.TextMuted);
            }
        }

        public void ShowMessage(string key)
        {
            message.gameObject.SetActive(key != null);
            if (key != null) message.SetKey(key);
        }

        public void SetBusy(bool busy)
        {
            freeButton.interactable = !busy;
            closeButton.interactable = !busy;
            foreach (var button in adButtons) button.interactable = !busy;
        }

        private void OnFree() => FreeClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
