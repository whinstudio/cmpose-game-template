using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // A springy squash and stretch of a transform (a landing, a hit, a tap): squashed along its up axis and widened by
    // amount, wobbling back to its scale. Game time. For objects whose scale code does not set every frame; the scale it
    // had when the first squash started is restored at the end.
    //   SquashStretch.Play(piece.transform);
    public sealed class SquashStretch : MonoBehaviour
    {
        public const float DefaultAmount = 0.22f;
        public const float DefaultDuration = 0.45f;
        // Wobbles over the duration
        private const float Wobbles = 2.5f;

        private Vector3 rest;
        private float amount;
        private float duration;
        private float elapsed;
        private bool playing;

        public bool Playing => playing;

        public static SquashStretch Play(Transform target, float amount = DefaultAmount, float duration = DefaultDuration)
        {
            if (target == null) return null;
            var squash = target.GetComponent<SquashStretch>();
            if (squash == null) squash = target.gameObject.AddComponent<SquashStretch>();
            squash.Begin(amount, duration);
            return squash;
        }

        public void Begin(float value, float seconds)
        {
            if (!playing) rest = transform.localScale;
            amount = value;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            playing = true;
        }

        public void Advance(float seconds)
        {
            if (!playing) return;
            elapsed += seconds;
            var t = Mathf.Clamp01(elapsed / duration);
            // Squash first (negative), then a smaller stretch, dying out
            var s = -amount * Mathf.Cos(t * Wobbles * Mathf.PI) * (1f - t) * (1f - t);
            var side = 1f - s * 0.5f;
            transform.localScale = new Vector3(rest.x * side, rest.y * (1f + s), rest.z * side);
            if (t < 1f) return;
            transform.localScale = rest;
            playing = false;
        }

        private void Update() => Advance(Time.deltaTime);

        private void OnDisable()
        {
            if (!playing) return;
            transform.localScale = rest;
            playing = false;
        }
    }
}
