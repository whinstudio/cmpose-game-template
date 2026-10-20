using System;
using System.Collections.Generic;
using System.Globalization;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the Game scene's shell HUD (SafeArea/ShellHud/Content/BoosterBar), so it hides with
    // the HUD under the end cards: up to Slots round buttons along the bottom, each with its icon (or the fallback
    // letter in the button's label) and a count badge ("+" with none in stock)
    public sealed class BoosterBarView : MonoBehaviour, IBoosterBarView
    {
        public const int Count = 3;

        [SerializeField] private GameObject content;
        [SerializeField] private Button[] slots;
        [SerializeField] private FallbackIcon[] icons;
        [SerializeField] private LocalizedText[] counts;

        public event Action<int> Clicked;

        public int Slots => slots.Length;
        public GameObject Content => content;

        private void Awake()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var index = i;
                slots[i].onClick.AddListener(() => Clicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            foreach (var slot in slots) slot.onClick.RemoveAllListeners();
        }

        public void Show(IReadOnlyList<BoosterSlot> list)
        {
            content.SetActive(list.Count > 0);
            for (var i = 0; i < slots.Length; i++)
            {
                var visible = i < list.Count;
                slots[i].gameObject.SetActive(visible);
                if (visible) icons[i].ShowKey(list[i].Icon, list[i].NameKey);
            }
        }

        public void SetCount(int index, int count)
        {
            if (index < 0 || index >= counts.Length) return;
            counts[index].SetLiteral(count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "+");
        }
    }
}
