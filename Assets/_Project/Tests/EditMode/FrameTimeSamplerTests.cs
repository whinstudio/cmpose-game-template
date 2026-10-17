using Kturker.UnityBase.App.Game;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class FrameTimeSamplerTests
    {
        [Test]
        public void Skips_the_warmup_and_reports_the_window_once()
        {
            var sampler = new FrameTimeSampler();
            // A slow start (loading) that must not count
            Assert.That(sampler.Add(FrameTimeSampler.Warmup), Is.Null);
            string line = null;
            var frames = 0;
            while (line == null && frames < 10000)
            {
                line = sampler.Add(0.02f);
                frames++;
            }
            Assert.That(line, Is.EqualTo($"CMPOSE_FRAMES avg=20.0ms fps=50 frames={frames}"));
            Assert.That(frames * 0.02f, Is.EqualTo(FrameTimeSampler.Window).Within(0.021f));
            Assert.That(sampler.Add(0.02f), Is.Null, "once");
        }

        [Test]
        public void Ignores_frames_without_time()
        {
            var sampler = new FrameTimeSampler();
            sampler.Add(FrameTimeSampler.Warmup);
            Assert.That(sampler.Add(0f), Is.Null);
            Assert.That(sampler.Add(-1f), Is.Null);
            Assert.That(sampler.Add(FrameTimeSampler.Window), Is.EqualTo("CMPOSE_FRAMES avg=5000.0ms fps=0 frames=1"));
        }
    }
}
