namespace Kturker.UnityBase.Core.Presentation
{
    // Durations (seconds, unscaled time) and curves of one MotionProfile; UiMotion.Timings gives them
    public readonly struct MotionTimings
    {
        public MotionTimings(float press, float release, float transition, float sheet, float countUp, float breath,
            Ease enter, Ease releaseEase, float pressScale)
        {
            Press = press;
            Release = release;
            Transition = transition;
            Sheet = sheet;
            CountUp = countUp;
            Breath = breath;
            Enter = enter;
            ReleaseEase = releaseEase;
            PressScale = pressScale;
        }

        // A button going down under the finger (OutCubic)
        public float Press { get; }
        // Its way back up (ReleaseEase); a switch's knob, a segment's indicator
        public float Release { get; }
        // A screen sliding and fading in or out (Enter)
        public float Transition { get; }
        // A sheet rising from the bottom or a modal popping in (Enter)
        public float Sheet { get; }
        // A number counting up to its target
        public float CountUp { get; }
        // One in-and-out cycle of an idle pulse
        public float Breath { get; }
        public Ease Enter { get; }
        public Ease ReleaseEase { get; }
        // The scale of a pressed button
        public float PressScale { get; }

        // The longest a single kit animation takes under this profile
        public float Longest => System.Math.Max(System.Math.Max(Press + Release, Transition), System.Math.Max(Sheet, CountUp));
    }
}
