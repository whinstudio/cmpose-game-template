namespace Kturker.UnityBase.Features.Economy
{
    // Without the saveSystem flag the wallet lasts until the app closes
    public sealed class MemoryEconomyStore : IEconomyStore
    {
        private EconomyState state;

        public EconomyState Load() => state;

        public void Save(EconomyState saved) => state = saved;
    }
}
