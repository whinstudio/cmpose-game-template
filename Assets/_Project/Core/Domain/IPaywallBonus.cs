using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Domain
{
    // A line the paywall shows under its packages (the economy's welcome bundle): LineKey's text with its {0} filled
    // by the parts, each written "+<value> <text of its key>" and joined with ", " in the order added ("Bonus: +600
    // coins, +3 Bomb"). LineKey null: nothing to show.
    public interface IPaywallBonus
    {
        string LineKey { get; }
        IReadOnlyDictionary<string, string> Args { get; }
    }
}
