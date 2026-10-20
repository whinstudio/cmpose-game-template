using System;
using Kturker.UnityBase.Features.Shell.GameShell;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // Moves the wheel on as the game shell reports finished runs and levels; does nothing in a game without a wheel
    public sealed class WheelRunCounter : IInitializable, IDisposable
    {
        private readonly ShellRunEvents runs;
        private readonly Wheel wheel;
        private bool listening;

        public WheelRunCounter(ShellRunEvents runs, Wheel wheel)
        {
            this.runs = runs;
            this.wheel = wheel;
        }

        public void Initialize()
        {
            if (listening || !wheel.Configured) return;
            runs.RunEnded += OnRunEnded;
            listening = true;
        }

        public void Dispose()
        {
            if (!listening) return;
            runs.RunEnded -= OnRunEnded;
            listening = false;
        }

        private void OnRunEnded(bool won) => wheel.RunEnded(won, runs.Mode);
    }
}
