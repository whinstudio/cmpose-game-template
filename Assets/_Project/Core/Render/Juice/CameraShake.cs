using UnityEngine;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Core.Render
{
    // A short decaying shake of a camera (the main one by default), on game time: it stops with the game at timeScale 0.
    // The offset is added only while the camera renders and taken off after, so code that moves the camera every frame
    // (a follow camera) keeps working. Shakes add up to a cap.
    //   CameraShake.Shake(0.25f);
    public sealed class CameraShake : MonoBehaviour
    {
        public const float DefaultDuration = 0.3f;
        // Strength cap (world units at the camera) and shakes per second
        private const float MaxStrength = 1f;
        private const float Frequency = 22f;

        private float strength;
        private float duration;
        private float elapsed;
        private float seed;
        private Vector3 applied;
        private Camera target;

        public bool Shaking => elapsed < duration && strength > 0f;
        // The offset of the frame being drawn
        public Vector3 Offset { get; private set; }

        public static CameraShake Shake(float strength, float duration = DefaultDuration, Camera camera = null)
        {
            if (camera == null) camera = Camera.main;
            if (camera == null) return null;
            var shake = camera.GetComponent<CameraShake>();
            if (shake == null) shake = camera.gameObject.AddComponent<CameraShake>();
            shake.Add(strength, duration);
            return shake;
        }

        public void Add(float amount, float seconds)
        {
            var remaining = Shaking ? strength * (1f - elapsed / duration) : 0f;
            strength = Mathf.Min(MaxStrength, remaining + Mathf.Max(0f, amount));
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            // A fresh stretch of noise for each shake (looks random, stays deterministic)
            seed = Mathf.Repeat(seed + 17.31f, 1000f);
        }

        // Moves the shake on by seconds (Update does with the game's delta time)
        public void Advance(float seconds)
        {
            if (!Shaking)
            {
                Offset = Vector3.zero;
                return;
            }
            elapsed += seconds;
            var t = Mathf.Clamp01(elapsed / duration);
            var falloff = (1f - t) * (1f - t);
            var time = elapsed * Frequency;
            Offset = new Vector3(Mathf.PerlinNoise(seed, time) - 0.5f, Mathf.PerlinNoise(seed + 31f, time) - 0.5f, 0f) * (2f * strength * falloff);
        }

        private void Awake() => target = GetComponent<Camera>();

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            Offset = Vector3.zero;
        }

        private void Update() => Advance(Time.deltaTime);

        private void OnBegin(ScriptableRenderContext context, Camera camera)
        {
            if (camera != target || Offset == Vector3.zero) return;
            applied = transform.rotation * Offset;
            transform.position += applied;
        }

        private void OnEnd(ScriptableRenderContext context, Camera camera)
        {
            if (camera != target || applied == Vector3.zero) return;
            transform.position -= applied;
            applied = Vector3.zero;
        }
    }
}
