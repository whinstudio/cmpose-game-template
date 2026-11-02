using Kturker.UnityBase.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Settings
{
    // The settings card's Body: Rows over Actions while the card's top canvas is portrait, side by side (Rows left,
    // Actions right, equal widths, tops aligned) while it is wider than tall (Landscape), so the busiest card keeps
    // touch-sized controls on a landscape phone. It also sets the card's width: its preferred width is the card's (the
    // card's ContentSizeFitter) less the card's padding: SideInset clear of the safe area's sides, at most
    // LandscapeWidth in landscape. In landscape the footer leaves the column for the title's row (Place): its links on
    // the left, its version on the right ending clear of Close's touch area, the title in the middle (the footer's
    // KitTextGroup keeps clear of it) and a message in the links' place, left of the title, so the card is a row
    // shorter; under Body the card's bottom padding is then its side padding (the portrait footer's own is smaller). Chosen again on every layout pass, and before a draw when the top canvas resized.
    [ExecuteAlways]
    public sealed class SettingsCardLayout : HorizontalOrVerticalLayoutGroup
    {
        // Units between the card and the safe area's sides (room for the Close button that overhangs the card's corner)
        public const float SideInset = 100f;
        // The landscape card's widest
        public const float LandscapeWidth = 1560f;

        // The card's footer row, its title, Close and the footer's message (Configure)
        [SerializeField] private RectTransform footer;
        [SerializeField] private TMP_Text title;
        [SerializeField] private RectTransform close;
        [SerializeField] private RectTransform message;
        // The card's bottom padding (KitCard.Pad) under the portrait footer
        [SerializeField] private int portraitBottom;

        private Vector2 seen;
        private readonly Vector3[] corners = new Vector3[4];

        // The top canvas is wider than tall: Rows and Actions side by side
        public bool Landscape { get; private set; }

        public void Configure(RectTransform footerRow, TMP_Text cardTitle, RectTransform closeButton, RectTransform footerMessage,
            int footerBottom)
        {
            portraitBottom = footerBottom;
            footer = footerRow;
            title = cardTitle;
            close = closeButton;
            message = footerMessage;
            SetDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.willRenderCanvases += Check;
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= Check;
            base.OnDisable();
        }

        public override void CalculateLayoutInputHorizontal()
        {
            Choose();
            base.CalculateLayoutInputHorizontal();
            ReportCardWidth();
        }

        public override void CalculateLayoutInputVertical() => CalcAlongAxis(1, !Landscape);

        // The children's own widths first: with the card's width as their total preferred width, a row of them (landscape)
        // would have nothing left over to stretch across and stay at their preferred width, 0
        public override void SetLayoutHorizontal()
        {
            CalcAlongAxis(0, !Landscape);
            SetChildrenAlongAxis(0, !Landscape);
            ReportCardWidth();
            Place();
        }

        public override void SetLayoutVertical() => SetChildrenAlongAxis(1, !Landscape);

        // The card's ContentSizeFitter takes the card's width from here
        private void ReportCardWidth() => SetLayoutInputForAxis(0f, Mathf.Max(0f, CardWidth() - CardPadding()), 1f, 0);

        // The card's column lays the footer out in portrait only: chosen before the column measures its children
        private void Choose()
        {
            var top = Top();
            seen = top != null ? top.rect.size : Vector2.zero;
            Landscape = seen.x > seen.y;
            if (footer == null) return;
            var element = footer.GetComponent<LayoutElement>();
            if (element != null && element.ignoreLayout != Landscape) element.ignoreLayout = Landscape;
            var group = footer.GetComponent<KitTextGroup>();
            if (group != null) group.KeepClearOf(Landscape ? title : null);
            PadBottom();
        }

        // The card's bottom padding: the side padding in landscape, portraitBottom under the footer (a skin's frame still
        // widens either, KitCard)
        private void PadBottom()
        {
            var card = transform.parent != null ? transform.parent.GetComponent<KitCard>() : null;
            if (card == null) return;
            var padding = card.Padding;
            var bottom = Landscape ? padding.left : portraitBottom;
            if (padding.bottom == bottom) return;
            card.Pad(new RectOffset(padding.left, padding.right, padding.top, bottom), null, card.FrameLimit);
        }

        // In landscape the footer over the title's row, from the column's left padding to Close's touch area (spacing
        // clear), and the message in it from its left end to the title's text (spacing clear); in portrait the message
        // over the whole footer
        private void Place()
        {
            if (footer == null || title == null || message == null) return;
            if (!Landscape)
            {
                Set(message, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                return;
            }
            var card = transform.parent as RectTransform;
            var column = card != null ? card.GetComponent<LayoutGroup>() : null;
            if (column == null) return;
            var padding = column.padding;
            var row = footer.GetComponent<HorizontalOrVerticalLayoutGroup>();
            var spacing = row != null ? row.spacing : KitLayout.Gap;
            var box = card.rect;
            var right = (float)padding.right;
            if (close != null && close.gameObject.activeInHierarchy) right = Mathf.Max(right, box.xMax - CloseLeft(card) + spacing);
            var height = LayoutUtility.GetPreferredHeight(title.rectTransform);
            if (footer.pivot != new Vector2(0.5f, 1f)) footer.pivot = new Vector2(0.5f, 1f);
            Set(footer, new Vector2(0f, 1f), Vector2.one, new Vector2(padding.left, -padding.top - height), new Vector2(-right, -padding.top));
            // The title's text in the middle of the column's inner width
            var centre = box.xMin + padding.left + (box.width - padding.horizontal) / 2f;
            var clear = centre - title.GetPreferredValues(title.text ?? string.Empty).x / 2f - spacing;
            var width = box.width - padding.left - right;
            var room = Mathf.Max(0f, clear - (box.xMin + padding.left));
            Set(message, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(Mathf.Min(0f, room - width), 0f));
        }

        // The left edge of Close and its touch area in the card's units
        private float CloseLeft(RectTransform card)
        {
            var left = float.PositiveInfinity;
            foreach (var rect in close.GetComponentsInChildren<RectTransform>())
            {
                rect.GetWorldCorners(corners);
                foreach (var corner in corners) left = Mathf.Min(left, card.InverseTransformPoint(corner).x);
            }
            return left;
        }

        // Only what changed: a layout pass sets these on every pass
        private static void Set(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect.anchorMin != anchorMin) rect.anchorMin = anchorMin;
            if (rect.anchorMax != anchorMax) rect.anchorMax = anchorMax;
            if (rect.offsetMin != offsetMin) rect.offsetMin = offsetMin;
            if (rect.offsetMax != offsetMax) rect.offsetMax = offsetMax;
        }

        // The card is SideInset clear of its parent's (the safe area's) sides; in landscape no wider than LandscapeWidth
        private float CardWidth()
        {
            var card = transform.parent as RectTransform;
            var area = card != null ? card.parent as RectTransform : null;
            if (area == null) return 0f;
            var width = area.rect.width - 2f * SideInset;
            return Landscape ? Mathf.Min(width, LandscapeWidth) : width;
        }

        private float CardPadding()
        {
            var column = transform.parent != null ? transform.parent.GetComponent<LayoutGroup>() : null;
            return column != null ? column.padding.horizontal : 0f;
        }

        // The root canvas, or the topmost RectTransform without one (a test's stage)
        private RectTransform Top()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return (RectTransform)canvas.rootCanvas.transform;
            RectTransform top = null;
            for (var t = transform; t != null; t = t.parent)
                if (t is RectTransform rect) top = rect;
            return top;
        }

        // A rotation resizes the canvas, not this box: lay out again for it
        private void Check()
        {
            var top = Top();
            if (top != null && top.rect.size != seen) SetDirty();
        }
    }
}
