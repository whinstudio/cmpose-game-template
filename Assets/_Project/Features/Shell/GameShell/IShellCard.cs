using System;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // An end card (a centred modal): its buttons raise Chosen; Continue shows only after ShowContinue(true)
    public interface IShellCard
    {
        event Action<EndChoice> Chosen;
        bool IsShown { get; }

        // free: premium's continue, without "(ad)"
        void ShowContinue(bool shown, bool free);

        void Hide();
    }
}
