using Kturker.UnityBase.Core.Save;

namespace Kturker.UnityBase.Features.Economy
{
    // saveSystem flag: the wallet survives restarts (slot 'wallet')
    public sealed class SavedEconomyStore : IEconomyStore
    {
        public static readonly SaveSchema<EconomyState> Schema = new SaveSchema<EconomyState>("wallet", 1);

        private readonly ISaveStore store;

        public SavedEconomyStore(ISaveStore store) => this.store = store;

        public EconomyState Load() => store.Load(Schema);

        public void Save(EconomyState state) => store.Save(Schema, state);
    }
}
