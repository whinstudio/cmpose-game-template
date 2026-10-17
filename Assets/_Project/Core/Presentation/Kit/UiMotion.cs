using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The template's tweens (named apart from UnityEngine.Motion, the animation clip type): unscaled time (they run
    // while the game is paused at timeScale 0), on UniTask, no allocation per frame. Kit components step their own
    // state in Update with the same curves; these awaitable ones are for one-off moves in game and view code. A
    // destroyed target ends a tween quietly; a cancelled token throws OperationCanceledException as UniTask does.
    public static class UiMotion
    {
        public static MotionTimings Timings(MotionProfile profile)
        {
            switch (profile)
            {
                case MotionProfile.Calm:
                    return new MotionTimings(0.12f, 0.30f, 0.38f, 0.48f, 1.0f, 2.4f, Ease.OutCubic, Ease.OutCubic, 0.95f);
                case MotionProfile.Snappy:
                    return new MotionTimings(0.07f, 0.22f, 0.24f, 0.32f, 0.7f, 1.8f, Ease.OutQuint, Ease.OutBack, 0.93f);
                case MotionProfile.Bouncy:
                    return new MotionTimings(0.08f, 0.42f, 0.30f, 0.45f, 0.8f, 1.4f, Ease.Spring, Ease.Spring, 0.90f);
                default: throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
            }
        }

        // The theme's profile, Snappy before a theme is bound
        public static MotionProfile ProfileOf(IThemeSource theme)
        {
            if (theme == null) return MotionProfile.Snappy;
            var style = theme.Style;
            return style != null ? style.Motion : MotionProfile.Snappy;
        }

        // The curve at t (0..1, clamped): 0 at the start, exactly 1 at the end
        public static float Evaluate(Ease ease, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.OutCubic: return 1f - Cube(1f - t);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Cube(-2f * t + 2f) / 2f;
                case Ease.OutQuint:
                {
                    var u = 1f - t;
                    return 1f - u * u * u * u * u;
                }
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    var u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                // About 18 % overshoot at a third of the way, a smaller swing back, at rest at the end
                case Ease.Spring: return 1f - (1f - t) * Mathf.Exp(-4f * t) * Mathf.Cos(3f * Mathf.PI * t);
                default: throw new ArgumentOutOfRangeException(nameof(ease), ease, null);
            }
        }

        // 0 at both ends, a decaying wobble between (the first swing reaches about 0.75): scale = 1 + amount * Punch(t)
        public static float Punch(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            var u = 1f - t;
            return Mathf.Sin(3f * Mathf.PI * t) * u * u;
        }

        public static async UniTask Scale(Transform target, Vector3 to, float duration, Ease ease, CancellationToken token = default)
        {
            if (target == null) return;
            var from = target.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                target.localScale = Vector3.LerpUnclamped(from, to, Evaluate(ease, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (target == null) return;
                elapsed += Time.unscaledDeltaTime;
            }
            target.localScale = to;
        }

        // A damped spring to a scale (a pop-in from 0, a settle after a hit)
        public static UniTask Spring(Transform target, Vector3 to, float duration, CancellationToken token = default) =>
            Scale(target, to, duration, Ease.Spring, token);

        // Moves a RectTransform's anchoredPosition
        public static async UniTask Move(RectTransform target, Vector2 to, float duration, Ease ease, CancellationToken token = default)
        {
            if (target == null) return;
            var from = target.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, Evaluate(ease, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (target == null) return;
                elapsed += Time.unscaledDeltaTime;
            }
            target.anchoredPosition = to;
        }

        public static async UniTask Fade(CanvasGroup target, float to, float duration, Ease ease, CancellationToken token = default)
        {
            if (target == null) return;
            var from = target.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                target.alpha = Mathf.LerpUnclamped(from, to, Evaluate(ease, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (target == null) return;
                elapsed += Time.unscaledDeltaTime;
            }
            target.alpha = to;
        }

        // Swells the scale by amount (0.15 = 15 %) and wobbles back to where it was
        public static async UniTask Punch(Transform target, float amount, float duration, CancellationToken token = default)
        {
            if (target == null) return;
            var rest = target.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                target.localScale = rest * (1f + amount * Punch(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (target == null) return;
                elapsed += Time.unscaledDeltaTime;
            }
            target.localScale = rest;
        }

        private static float Cube(float v) => v * v * v;
    }
}
