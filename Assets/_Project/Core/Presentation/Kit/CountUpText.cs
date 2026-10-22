using System;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // A number that counts to its target on unscaled time (a score, a reward) and pops when it gets there. The text is
    // format with the value as {0} (TMP SetText: "{0:0}" is a whole number, "+{0:0}" a gain), no string per frame;
    // TMP formats a float, so values are exact up to 16 777 216. With digits (UseDigits) and a game skin's digit sprites
    // (GameSkin.Digits), a whole number ("{0:0}") of 0 or more is drawn in them instead and the text is hidden.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CountUpText : KitElement
    {
        private const float PopAmount = 0.12f;
        private const float PopSeconds = 0.3f;

        private const string WholeNumber = "{0:0}";

        [SerializeField] private string format = WholeNumber;
        [SerializeField] private KitDigits digits;
        private TMP_Text text;
        private KitTween count;
        private float popElapsed = -1f;
        private int shown = int.MinValue;

        public int Value { get; private set; }
        public int Target { get; private set; }
        public bool Counting => count.Running;
        public string Format => format;
        public KitDigits Digits => digits;

        public event Action Reached;

        public void SetFormat(string value)
        {
            format = string.IsNullOrEmpty(value) ? WholeNumber : value;
            shown = int.MinValue;
            Show(Value);
        }

        // Where the count shows in the skin's digit sprites (a stretched child)
        public void UseDigits(KitDigits target)
        {
            digits = target;
            shown = int.MinValue;
            Show(Value);
        }

        // Shows the value at once
        public void SetValue(int value)
        {
            Target = value;
            count.Snap(value);
            Show(value);
        }

        // Counts from the shown value to target in the profile's CountUp time
        public void CountTo(int target) => CountTo(target, Timings.CountUp);

        public void CountTo(int target, float seconds)
        {
            Target = target;
            if (target == Value)
            {
                count.Snap(target);
                Show(target);
                return;
            }
            count.Start(Value, target, seconds, Ease.OutCubic);
            if (!count.Running) Arrive();
        }

        // Steps the count by dt seconds (Update calls it with the unscaled delta)
        public void Advance(float dt)
        {
            if (count.Step(dt))
            {
                Show(Mathf.RoundToInt(count.Value));
                if (!count.Running) Arrive();
            }
            if (popElapsed < 0f) return;
            popElapsed += dt;
            var t = popElapsed / PopSeconds;
            if (t >= 1f) popElapsed = -1f;
            var value = 1f + PopAmount * UiMotion.Punch(t);
            transform.localScale = new Vector3(value, value, 1f);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            if (count.Running) SetValue(Target);
            popElapsed = -1f;
            transform.localScale = Vector3.one;
        }

        private void Arrive()
        {
            Show(Target);
            popElapsed = 0f;
            Reached?.Invoke();
        }

        private void Show(int value)
        {
            Value = value;
            if (value == shown) return;
            shown = value;
            if (text == null) text = GetComponent<TMP_Text>();
            text.SetText(format, value);
            if (digits == null) return;
            var sprites = GameSkin.Current?.Digits;
            var drawn = sprites != null && value >= 0 && format == WholeNumber;
            if (drawn) digits.Show(value, sprites);
            else digits.Hide();
            text.enabled = !drawn;
        }

        // The skin is read again (its digits may have come or gone)
        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (digits == null) return;
            shown = int.MinValue;
            Show(Value);
        }
    }
}
