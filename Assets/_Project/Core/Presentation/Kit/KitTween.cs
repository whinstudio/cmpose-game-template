namespace Kturker.UnityBase.Core.Presentation
{
    // One running 0..1 progress for the kit's Update-driven animations: a value type, nothing allocated per run
    public struct KitTween
    {
        private float from;
        private float to;
        private float elapsed;
        private float duration;
        private Ease ease;

        public bool Running { get; private set; }
        // The eased value (from..to, beyond while an OutBack or Spring overshoots)
        public float Value { get; private set; }
        public float Target => to;

        public void Start(float start, float end, float seconds, Ease curve)
        {
            from = start;
            to = end;
            elapsed = 0f;
            duration = seconds;
            ease = curve;
            Running = true;
            Value = start;
            if (seconds <= 0f) Snap(end);
        }

        public void Snap(float value)
        {
            from = value;
            to = value;
            Value = value;
            Running = false;
        }

        // Advances by dt seconds; false once at rest
        public bool Step(float dt)
        {
            if (!Running) return false;
            elapsed += dt;
            var t = elapsed / duration;
            if (t >= 1f)
            {
                Value = to;
                Running = false;
                return true;
            }
            Value = from + (to - from) * UiMotion.Evaluate(ease, t);
            return true;
        }
    }
}
