using System;
using Kturker.UnityBase.Features.Shell.GameShell;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // The game shell's counter pill shows the wallet's currency with its mark: written at the start, after every Begin
    // (Begin hides the counter) and on every change of the wallet. Nothing with the economy off (the pill keeps its
    // plain dot).
    public sealed class EconomyHud : IStartable, IDisposable
    {
        private readonly GameShell shell;
        private readonly IEconomy economy;
        private readonly EconomyCurrency currency;
        private bool listening;

        public EconomyHud(GameShell shell, IEconomy economy, EconomyCurrency currency)
        {
            this.shell = shell;
            this.economy = economy;
            this.currency = currency;
        }

        public void Start()
        {
            if (!economy.Enabled || listening) return;
            shell.Begun += Write;
            economy.Changed += Write;
            listening = true;
            Write();
        }

        public void Dispose()
        {
            if (!listening) return;
            shell.Begun -= Write;
            economy.Changed -= Write;
            listening = false;
        }

        private void Write()
        {
            shell.SetCounterIcon(currency.Mark);
            shell.SetCounter(economy.Coins);
        }
    }
}
