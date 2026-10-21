using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Keeps its RectTransform inside Screen.safeArea (notch, home indicator), above ReservedBottom and below
    // ReservedTop, and follows rotation and reservation changes (a reservation change applies at once, not on the next
    // Update, so a screen revealed right after it already has the new room)
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private static float reservedBottomPixels;
        private static float reservedTopPixels;

        // Pixels at the bottom of the screen that a native overlay covers (the ads banner); content stays above them
        public static float ReservedBottom
        {
            get => reservedBottomPixels;
            set
            {
                if (value == reservedBottomPixels) return;
                reservedBottomPixels = value;
                ReservationChanged?.Invoke();
            }
        }

        // Pixels at the top of the screen that a native overlay covers (the ads banner); content stays below them
        public static float ReservedTop
        {
            get => reservedTopPixels;
            set
            {
                if (value == reservedTopPixels) return;
                reservedTopPixels = value;
                ReservationChanged?.Invoke();
            }
        }

        // After ReservedBottom or ReservedTop changed; every enabled SafeArea has applied it already
        public static event Action ReservationChanged;

        private RectTransform rect;
        private Rect applied;
        private float reserved;
        private float reservedTop;
        private Vector2Int screen;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => ReservationChanged += Apply;

        private void OnDisable() => ReservationChanged -= Apply;

        private void Update()
        {
            if (Screen.safeArea != applied || reserved != ReservedBottom || reservedTop != ReservedTop || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        // Screen-fraction anchors of the safe rect with its bottom raised by reservedBottom pixels (never above its top)
        // and its top lowered by reservedTop pixels (never below the raised bottom)
        public static (Vector2 Min, Vector2 Max) Anchors(Rect safe, Vector2Int screen, float reservedBottom, float reservedTop = 0f)
        {
            var bottom = Mathf.Min(safe.yMin + Mathf.Max(0f, reservedBottom), safe.yMax);
            var top = Mathf.Max(safe.yMax - Mathf.Max(0f, reservedTop), bottom);
            return (new Vector2(safe.xMin / screen.x, bottom / screen.y), new Vector2(safe.xMax / screen.x, top / screen.y));
        }

        private void Apply()
        {
            applied = Screen.safeArea;
            reserved = ReservedBottom;
            reservedTop = ReservedTop;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var (min, max) = Anchors(applied, screen, reserved, reservedTop);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
