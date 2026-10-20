using System;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // A run or level that ended for good: the player chose something other than Continue on its end card. true for a
    // won level; false for a game over and a failed level.
    public interface IShellRunEvents
    {
        event Action<bool> RunEnded;
    }

    // The root scope's relay (the economy listens to it): every scene's GameShell reports here, with the run's mode
    public sealed class ShellRunEvents : IShellRunEvents
    {
        public event Action<bool> RunEnded;

        // The mode of the run that ended last
        public ShellMode Mode { get; private set; }

        public void Report(ShellMode mode, bool won)
        {
            Mode = mode;
            RunEnded?.Invoke(won);
        }
    }
}
