using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.Features.Economy;
using Kturker.UnityBase.Features.Shell.Purchases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    // The shop prefab (SceneBuilder): a slot for every row a shop may have, hidden ones leaving no gap; the coin packs
    // share the grid's width; a failed load trades the rows for Try again; the column scrolls
    public sealed class ShopViewTests
    {
        private ShopView view;

        [SetUp]
        public void SetUp() => view = Object.Instantiate(Resources.Load<ShopView>(ShopHost.ResourcePath));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(view.gameObject);

        private Transform Content => view.transform.Find("Panel/SafeArea/Scroll/Viewport/Content");

        private Transform Part(string name) => Content.Find(name);

        private static ShopRow Row(string id, int? tag = null) => new ShopRow(id, PurchasesKeys.RowTitle(id), "+400 coins", "$1.99", tag);

        private static ShopRow Pack(int i) => new ShopRow($"coins_{i}", null, "100 coins", "$0.99", null);

        private static IReadOnlyList<ShopRow> Packs(int count) => Enumerable.Range(1, count).Select(Pack).ToList();

        [Test]
        public void There_is_a_slot_for_every_row_a_shop_may_have()
        {
            Assert.That(view.MaxRows + view.MaxPacks, Is.EqualTo(EconomyConfig.MaxShopRows));
            Assert.That(view.MaxPacks, Is.EqualTo(6));
            Assert.That(view.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
        }

        [Test]
        public void Only_the_rows_given_show_and_a_tag_only_where_set()
        {
            view.ShowRows(new[] { Row(ShopProducts.StarterPack, 90), Row(ShopProducts.RemoveAds) }, Packs(4), 2);

            for (var i = 0; i < view.MaxRows; i++) Assert.That(Part($"Row{i}").gameObject.activeSelf, Is.EqualTo(i < 2), $"row {i}");
            for (var i = 0; i < view.MaxPacks; i++) Assert.That(Part($"Packs/Pack{i}").gameObject.activeSelf, Is.EqualTo(i < 4), $"pack {i}");
            Assert.That(Part("Row0/Tag").gameObject.activeSelf, Is.True);
            Assert.That(Part("Row1/Tag").gameObject.activeSelf, Is.False);
            Assert.That(Part("Status").gameObject.activeSelf, Is.False);
            Assert.That(Part("Retry").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void No_packs_hide_the_grid()
        {
            view.ShowRows(new[] { Row(ShopProducts.RemoveAds) }, new ShopRow[0], 2);

            Assert.That(Part("Packs").gameObject.activeSelf, Is.False);
        }

        [TestCase(ShopPresenter.FullColumns, 6)]
        [TestCase(ShopPresenter.SimpleColumns, 4)]
        public void The_packs_share_the_grid_width_in_their_columns(int columns, int count)
        {
            // Laid out on a world-space canvas of the reference size (an overlay canvas takes the editor's screen size)
            Object.DestroyImmediate(view.GetComponent<CanvasScaler>());
            view.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)view.transform).sizeDelta = new Vector2(1080f, 1920f);
            view.ShowRows(new ShopRow[0], Packs(count), columns);
            var grid = Part("Packs").GetComponent<GridLayoutGroup>();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.transform);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Content);
            // Laid out now: the grid has its width and its cells share it (ShopPackGrid)
            view.ShowRows(new ShopRow[0], Packs(count), columns);

            var width = ((RectTransform)grid.transform).rect.width;
            Assert.That(width, Is.GreaterThan(0f));
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(columns));
            Assert.That(grid.cellSize.x * columns + grid.spacing.x * (columns - 1) + grid.padding.horizontal,
                Is.EqualTo(width).Within(0.5f));
        }

        [Test]
        public void A_failed_load_shows_the_status_and_try_again_instead_of_the_rows()
        {
            view.ShowRows(new[] { Row(ShopProducts.RemoveAds) }, Packs(2), 2);

            view.ShowFailed();

            Assert.That(Part("Row0").gameObject.activeSelf, Is.False);
            Assert.That(Part("Packs").gameObject.activeSelf, Is.False);
            Assert.That(Part("Status").gameObject.activeSelf, Is.True);
            Assert.That(Part("Retry").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Busy_disables_every_button()
        {
            view.ShowRows(new[] { Row(ShopProducts.RemoveAds) }, Packs(1), 2);

            view.SetBusy(true);

            Assert.That(view.GetComponentsInChildren<Button>(true).All(b => !b.interactable), Is.True);
            view.SetBusy(false);
            Assert.That(Part("Row0/Buy").GetComponent<Button>().interactable, Is.True);
        }

        [Test]
        public void The_wallet_hides_without_an_economy()
        {
            var wallet = view.transform.Find("Panel/SafeArea/TopBar/Wallet");

            view.ShowWallet(null, null);
            Assert.That(wallet.gameObject.activeSelf, Is.False);
            view.ShowWallet(120, null);
            Assert.That(wallet.gameObject.activeSelf, Is.True);
        }
    }
}
