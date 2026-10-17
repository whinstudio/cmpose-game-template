using System;

namespace Kturker.UnityBase.Core.Haptics
{
    // Android one-shot vibration per strength: duration in ms and amplitude (1-255); a waveform per event: off/on
    // durations in ms from the start, with an amplitude (0-255) per step
    public static class HapticPattern
    {
        public static long DurationMs(HapticStrength strength)
        {
            switch (strength)
            {
                case HapticStrength.Light: return 10;
                case HapticStrength.Medium: return 40;
                case HapticStrength.Heavy: return 80;
                default: throw new ArgumentOutOfRangeException(nameof(strength), strength, null);
            }
        }

        public static int Amplitude(HapticStrength strength)
        {
            switch (strength)
            {
                case HapticStrength.Light: return 80;
                case HapticStrength.Medium: return 160;
                case HapticStrength.Heavy: return 255;
                default: throw new ArgumentOutOfRangeException(nameof(strength), strength, null);
            }
        }

        public static long[] Timings(HapticEvent e)
        {
            switch (e)
            {
                // Two rising taps
                case HapticEvent.Success: return new long[] { 0, 30, 60, 40 };
                // Three hard buzzes
                case HapticEvent.Failure: return new long[] { 0, 60, 70, 60, 70, 90 };
                default: throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }

        public static int[] Amplitudes(HapticEvent e)
        {
            switch (e)
            {
                case HapticEvent.Success: return new[] { 0, 120, 0, 220 };
                case HapticEvent.Failure: return new[] { 0, 255, 0, 255, 0, 180 };
                default: throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }
    }
}
