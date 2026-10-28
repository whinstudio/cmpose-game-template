namespace Kturker.UnityBase.Core.Purchases
{
    // A finished purchase: the customer info after it and its transaction (null when the store sent none)
    public sealed class PurchaseReply
    {
        public PurchaseReply(CustomerSnapshot customer, StoreTransaction transaction)
        {
            Customer = customer;
            Transaction = transaction;
        }

        public CustomerSnapshot Customer { get; }
        public StoreTransaction Transaction { get; }
    }
}
