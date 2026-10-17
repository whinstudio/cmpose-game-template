using Kturker.UnityBase.Core.Domain;
using UnityEngine;
using VContainer.Unity;

namespace Kturker.UnityBase.App.Game
{
    // Logs a game scene's average frame time once (FrameTimeSampler), on unscaled time; the gate's --launch prints it
    // as an information row for the showcase APKs (no threshold)
    public sealed class FrameTimeMarker : ITickable
    {
        private readonly ILog log;
        private readonly FrameTimeSampler sampler = new FrameTimeSampler();

        public FrameTimeMarker(ILog log) => this.log = log;

        public void Tick()
        {
            var line = sampler.Add(Time.unscaledDeltaTime);
            if (line != null) log.Marker(line);
        }
    }
}
