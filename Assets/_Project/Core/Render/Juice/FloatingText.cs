using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Render
{
    // A short text that pops up at a point of the world, rises and fades ("+10", a combo): world-space TextMeshPro in
    // the style's display font, facing the camera, drawn over the world, pooled. Game time. The text is shown as given:
    // numbers, or a string the game took from its ILocalizer.
    //   FloatingText.Spawn(hit.point, "+10", WorldPalette.Color(1));
    public sealed class FloatingText : MonoBehaviour
    {
        public const float DefaultSize = 0.6f;
        public const float Lifetime = 0.9f;
        private const float Rise = 1.2f;
        private const float PopFrom = 0.4f;
        private const float PopTime = 0.18f;
        private const int SortingOrder = 500;

        private static readonly List<FloatingText> Pool = new List<FloatingText>();

        private TextMeshPro label;
        private Vector3 origin;
        private Color color;
        private float elapsed;

        public TextMeshPro Label => label;
        public bool Running => gameObject.activeSelf && elapsed < Lifetime;

        public static FloatingText Spawn(Vector3 position, string text, Color color, float size = DefaultSize)
        {
            var item = Take();
            item.Begin(position, text, color, size);
            return item;
        }

        private static FloatingText Take()
        {
            Pool.RemoveAll(p => p == null);
            foreach (var item in Pool)
                if (!item.gameObject.activeSelf) return item;
            var go = new GameObject("FloatingText");
            var created = go.AddComponent<FloatingText>();
            created.label = go.AddComponent<TextMeshPro>();
            created.label.alignment = TextAlignmentOptions.Center;
            created.label.textWrappingMode = TextWrappingModes.NoWrap;
            created.label.fontStyle = FontStyles.Normal;
            var style = Resources.Load<ThemeStyle>(ThemeStyle.ResourcePath);
            if (style != null && style.DisplayFont != null) created.label.font = style.DisplayFont;
            created.label.GetComponent<MeshRenderer>().sortingOrder = SortingOrder;
            Pool.Add(created);
            return created;
        }

        private void Begin(Vector3 position, string text, Color tint, float size)
        {
            origin = position;
            color = tint;
            elapsed = 0f;
            label.text = text;
            label.fontSize = size * 10f;
            gameObject.SetActive(true);
            Advance(0f);
        }

        public void Advance(float seconds)
        {
            elapsed += seconds;
            var t = Mathf.Clamp01(elapsed / Lifetime);
            var rise = 1f - (1f - t) * (1f - t);
            transform.position = origin + Vector3.up * (Rise * rise);
            var pop = Mathf.Clamp01(elapsed / PopTime);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(PopFrom, 1f, UiMotion.Evaluate(Ease.OutBack, pop));
            var view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;
            var fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            label.color = new Color(color.r, color.g, color.b, color.a * fade);
            if (t >= 1f) gameObject.SetActive(false);
        }

        private void Update() => Advance(Time.deltaTime);
    }
}
