using System.Globalization;

namespace Kturker.UnityBase.App.Game
{
    // The average frame time of a game scene: frames in the first Warmup seconds (loading, shader warm-up) are skipped,
    // then Window seconds are averaged; Add returns the marker line once, when the window is full, and null otherwise
    public sealed class FrameTimeSampler
    {
        public const float Warmup = 2f;
        public const float Window = 5f;

        private float skipped;
        private float total;
        private int frames;
        private bool done;

        public string Add(float seconds)
        {
            if (done || seconds <= 0f) return null;
            if (skipped < Warmup)
            {
                skipped += seconds;
                return null;
            }
            total += seconds;
            frames++;
            if (total < Window) return null;
            done = true;
            var average = total / frames * 1000f;
            return string.Format(CultureInfo.InvariantCulture, "CMPOSE_FRAMES avg={0:0.0}ms fps={1:0} frames={2}", average, 1000f / average, frames);
        }
    }
}
