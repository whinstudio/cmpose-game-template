using System;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Showcase.Common.Purchases
{
    // A row in the menu's extra slot (prefab under Resources; SceneBuilder builds it); hidden rows leave no gap
    public sealed class ShopButtonView : MonoBehaviour, IShopButtonView
    {
        public const string ResourcePath = "ShowcasePurchases/Shop";

        [SerializeField] private Button button;

        public event Action Clicked;

        public static ShopButtonView Spawn(Transform slot, UiBinder binder)
        {
            var prefab = Resources.Load<ShopButtonView>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException($"missing Resources/{ResourcePath}");
            var view = Instantiate(prefab, slot, false);
            binder.BindTree(view.gameObject);
            return view;
        }

        private void Awake() => button.onClick.AddListener(OnClick);

        private void OnDestroy() => button.onClick.RemoveListener(OnClick);

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        private void OnClick() => Clicked?.Invoke();
    }
}
