namespace Kturker.UnityBase.Core.Purchases
{
    // One product of the current offering; plain data, app code never sees a RevenueCat type
    public sealed class ShopProduct
    {
        public ShopProduct(string id, string priceString)
        {
            Id = id;
            PriceString = priceString;
        }

        // The RevenueCat package identifier, the same as the store product id (ShopProducts): PurchaseAsync takes it
        public string Id { get; }
        // Formatted by the store ("$4.99", "₺249,99"); the only price the game shows
        public string PriceString { get; }
    }
}
