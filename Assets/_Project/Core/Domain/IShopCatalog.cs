using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Domain
{
    // The rows a game's shop sells, in the screen's order, and what each pays (the economy's GameEconomy.json shop
    // section, EconomyShopCatalog). Without one bound the shop sells Remove Ads only.
    public interface IShopCatalog
    {
        IReadOnlyList<ShopRowContent> Rows { get; }
    }

    // A product id (remove_ads, starter_pack, no_ads_bundle, bundle_1..3, coins_1..6) and what it pays: coins, boosters
    // by id (empty for none) and an optional discount label in percent. Remove Ads pays nothing.
    public sealed class ShopRowContent
    {
        public ShopRowContent(string id, int coins, IReadOnlyDictionary<string, int> boosters, int? tag)
        {
            Id = id;
            Coins = coins;
            Boosters = boosters;
            Tag = tag;
        }

        public string Id { get; }
        public int Coins { get; }
        public IReadOnlyDictionary<string, int> Boosters { get; }
        public int? Tag { get; }

        public bool PaysSomething => Coins > 0 || Boosters.Count > 0;
    }
}
