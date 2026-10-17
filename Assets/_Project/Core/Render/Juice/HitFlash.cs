using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // A renderer flashes a colour (white by default) and fades back to its own, on game time. Meshes: through a
    // MaterialPropertyBlock's _BaseColor (the colour a property block already set, else the material's), the block's
    // other values kept; sprites: SpriteRenderer.color. The colour it had when the first flash started is restored.
    //   HitFlash.Flash(enemyRenderer);
    public sealed class HitFlash : MonoBehaviour
    {
        public const float DefaultDuration = 0.15f;

        private Renderer target;
        private SpriteRenderer sprite;
        private MaterialPropertyBlock block;
        private Color rest;
        private Color flash;
        private float duration;
        private float elapsed;
        private bool playing;

        public bool Playing => playing;

        public static HitFlash Flash(Renderer target, Color? color = null, float duration = DefaultDuration)
        {
            if (target == null) return null;
            var hit = target.GetComponent<HitFlash>();
            if (hit == null) hit = target.gameObject.AddComponent<HitFlash>();
            hit.Begin(color ?? Color.white, duration);
            return hit;
        }

        public void Begin(Color color, float seconds)
        {
            if (target == null)
            {
                target = GetComponent<Renderer>();
                sprite = target as SpriteRenderer;
                block = new MaterialPropertyBlock();
            }
            if (!playing) rest = Current();
            flash = color;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            playing = true;
            Paint(flash);
        }

        public void Advance(float seconds)
        {
            if (!playing) return;
            elapsed += seconds;
            var t = Mathf.Clamp01(elapsed / duration);
            Paint(Color.Lerp(flash, rest, t * t));
            if (t >= 1f) playing = false;
        }

        private void Update() => Advance(Time.deltaTime);

        private void OnDisable()
        {
            if (!playing || target == null) return;
            Paint(rest);
            playing = false;
        }

        private Color Current()
        {
            if (sprite != null) return sprite.color;
            target.GetPropertyBlock(block);
            if (block.HasColor(RenderMaterials.BaseColor)) return block.GetColor(RenderMaterials.BaseColor);
            var material = target.sharedMaterial;
            return material != null && material.HasProperty(RenderMaterials.BaseColor) ? material.GetColor(RenderMaterials.BaseColor) : Color.white;
        }

        private void Paint(Color color)
        {
            if (sprite != null)
            {
                sprite.color = color;
                return;
            }
            target.GetPropertyBlock(block);
            block.SetColor(RenderMaterials.BaseColor, color);
            target.SetPropertyBlock(block);
        }
    }
}
