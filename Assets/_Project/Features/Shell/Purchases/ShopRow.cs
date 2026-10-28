namespace Kturker.UnityBase.Features.Shell.Purchases
{
    // One row of the shop: its product id, title (key; null for a coin pack, whose content is its title), what it
    // pays ("No ads, +400 coins, +2 Bomb"), the store's price and an optional discount label in percent
    public readonly struct ShopRow
    {
        public ShopRow(string id, string titleKey, string content, string price, int? tag)
        {
            Id = id;
            TitleKey = titleKey;
            Content = content;
            Price = price;
            Tag = tag;
        }

        public string Id { get; }
        public string TitleKey { get; }
        public string Content { get; }
        public string Price { get; }
        public int? Tag { get; }
    }
}
