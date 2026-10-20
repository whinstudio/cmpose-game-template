using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows: a centred modal with up to Rows pressable booster rows (icon,
    // name, effect, the stock "×2" or the price with the currency's icon; a chosen row takes the surface variant and its
    // side bar), a hint and Play
    public sealed class BoosterPickerView : MonoBehaviour, IBoosterPickerView
    {
        public const int Rows = 3;

        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] rows;
        [SerializeField] private FallbackIcon[] rowIcons;
        [SerializeField] private LocalizedText[] rowNames;
        [SerializeField] private LocalizedText[] rowDescriptions;
        [SerializeField] private LocalizedText[] rowAmounts;
        [SerializeField] private FallbackIcon[] rowPriceIcons;
        [SerializeField] private GameObject[] rowSelected;
        [SerializeField] private Button playButton;

        public event Action<int> RowClicked;
        public event Action PlayClicked;

        public int MaxRows => rows.Length;

        private void Awake()
        {
            KitScreens.Hide(panel);
            playButton.onClick.AddListener(OnPlay);
            for (var i = 0; i < rows.Length; i++)
            {
                var index = i;
                rows[i].onClick.AddListener(() => RowClicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveListener(OnPlay);
            foreach (var row in rows) row.onClick.RemoveAllListeners();
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowRows(IReadOnlyList<PickerRow> list, EconomyCurrency currency)
        {
            var mark = currency.Mark;
            for (var i = 0; i < rows.Length; i++)
            {
                var visible = i < list.Count;
                rows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var row = list[i];
                rowNames[i].SetKey(row.NameKey);
                rowDescriptions[i].SetKey(row.DescriptionKey);
                rowIcons[i].ShowKey(row.Icon, row.NameKey);
                var priced = !row.ByAd && row.Owned <= 0;
                if (row.ByAd) rowAmounts[i].SetKey(EconomyKeys.Amount, 1);
                else if (!priced) rowAmounts[i].SetKey(EconomyKeys.Amount, row.Owned);
                else rowAmounts[i].SetKey(EconomyKeys.Price, row.Price);
                rowPriceIcons[i].gameObject.SetActive(priced);
                if (priced) rowPriceIcons[i].ShowCurrency(mark);
                rowSelected[i].SetActive(row.Selected);
                rows[i].GetComponent<ThemedGraphic>().SetRole(row.Selected ? ThemeRole.SurfaceVariant : ThemeRole.Track);
            }
        }

        private void OnPlay() => PlayClicked?.Invoke();
    }
}
