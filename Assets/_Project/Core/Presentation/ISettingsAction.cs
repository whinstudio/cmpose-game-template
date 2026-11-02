using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kturker.UnityBase.Core.Presentation
{
    // A settings card button that a feature flag contributes through DI (register it as ISettingsAction): RemoveAds,
    // Restore, SignIn or AdPrivacy. A hidden action leaves no gap in the grid. Changed fires after Visible may have changed.
    public interface ISettingsAction
    {
        SettingsButton Button { get; }
        bool Visible { get; }

        // The key of a message for the card (a restore's result), or null for none
        UniTask<string> PressAsync(CancellationToken ct);

        event Action Changed;
    }
}
