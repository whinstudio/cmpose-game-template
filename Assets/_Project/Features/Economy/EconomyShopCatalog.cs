using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Features.Economy
{
    // IShopCatalog from the economy's shop section, in the file's order. With the economy off, or a file without shop
    // rows (an old plan), the shop sells Remove Ads only.
    public sealed class EconomyShopCatalog : IShopCatalog
    {
        public const string RemoveAds = "remove_ads";

        public EconomyShopCatalog(EconomyConfig config) => Rows = Read(config);

        public IReadOnlyList<ShopRowContent> Rows { get; }

        // The row with this id, null when the shop does not sell it
        public ShopRowContent Row(string id)
        {
            foreach (var row in Rows)
            {
                if (row.Id == id) return row;
            }
            return null;
        }

        private static IReadOnlyList<ShopRowContent> Read(EconomyConfig config)
        {
            var rows = new List<ShopRowContent>();
            if (config != null)
            {
                foreach (var row in config.Shop.Rows) rows.Add(new ShopRowContent(row.Id, row.Coins, row.Boosters, row.Tag));
            }
            if (rows.Count == 0) rows.Add(new ShopRowContent(RemoveAds, 0, new Dictionary<string, int>(), null));
            return rows;
        }
    }
}
