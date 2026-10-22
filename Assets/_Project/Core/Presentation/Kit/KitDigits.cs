using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A whole number drawn in a game skin's digit sprites (GameSkin.Digits, 0..9): the digits sit side by side, centred,
    // as tall as this box and in their own aspect, all scaled down alike when the number is wider than the box. A pack's
    // digits have transparent edges (a 128 px square around a narrow glyph): the row is laid out by what each sprite
    // draws (its tight mesh's span; a full-rect sprite draws all of it), Gap of the height apart. The digit images are
    // made on first use and reused; CountUpText.UseDigits shows its count here.
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitDigits : MonoBehaviour
    {
        private readonly List<Image> pool = new List<Image>();
        private readonly int[] places = new int[10];

        // The space between two drawn digits, of the height
        public const float Gap = 0.04f;

        // The digits shown (0 while hidden)
        public int Count { get; private set; }

        public Image Digit(int index) => index >= 0 && index < pool.Count ? pool[index] : null;

        // number (0 or more) in digits (ten sprites, 0..9); anything else hides them
        public void Show(int number, IReadOnlyList<Sprite> digits)
        {
            if (digits == null || digits.Count < 10 || number < 0)
            {
                Hide();
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            // Digits made before a reload (an open scene) are still children
            if (pool.Count == 0) GetComponentsInChildren(true, pool);
            var count = 0;
            do
            {
                places[count++] = number % 10;
                number /= 10;
            } while (number > 0);
            Count = count;
            for (var i = 0; i < count; i++)
            {
                var image = i < pool.Count ? pool[i] : Make(i);
                image.sprite = digits[places[count - 1 - i]];
                if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            }
            for (var i = count; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
            Place();
        }

        public void Hide()
        {
            Count = 0;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnRectTransformDimensionsChange() => Place();

        private Image Make(int index)
        {
            var go = new GameObject($"Digit{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            pool.Add(image);
            return image;
        }

        // Each digit as tall as the box in its sprite's aspect, the drawn row centred on the box (the digits' anchor) and
        // shrunk to the box's width
        private void Place()
        {
            if (Count == 0) return;
            var box = ((RectTransform)transform).rect;
            var height = box.height;
            var width = height * Gap * (Count - 1);
            for (var i = 0; i < Count; i++)
            {
                var (from, to) = Drawn(pool[i].sprite);
                width += height * Aspect(pool[i].sprite) * (to - from);
            }
            var scale = width > box.width && width > 0f ? box.width / width : 1f;
            var x = -width * scale * 0.5f;
            for (var i = 0; i < Count; i++)
            {
                var size = new Vector2(height * Aspect(pool[i].sprite), height) * scale;
                var (from, to) = Drawn(pool[i].sprite);
                var rect = pool[i].rectTransform;
                rect.sizeDelta = size;
                // x is where this digit's drawn part starts
                rect.anchoredPosition = new Vector2(x - size.x * from + size.x * 0.5f, 0f);
                x += size.x * (to - from) + height * Gap * scale;
            }
        }

        private static float Aspect(Sprite sprite) =>
            sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;

        // The part of the sprite's width its mesh covers, 0..1 from the left (a full-rect sprite: 0 and 1)
        private static (float From, float To) Drawn(Sprite sprite)
        {
            if (sprite == null || sprite.rect.width <= 0f) return (0f, 1f);
            var vertices = sprite.vertices;
            if (vertices.Length == 0) return (0f, 1f);
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var vertex in vertices)
            {
                min = Mathf.Min(min, vertex.x);
                max = Mathf.Max(max, vertex.x);
            }
            var from = Mathf.Clamp01((min * sprite.pixelsPerUnit + sprite.pivot.x) / sprite.rect.width);
            var to = Mathf.Clamp01((max * sprite.pixelsPerUnit + sprite.pivot.x) / sprite.rect.width);
            return to > from ? (from, to) : (0f, 1f);
        }
    }
}
