using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Core.Presentation
{
    // Gives a group of single-line texts (a card's row labels, its button labels) one shared size: the largest, from max
    // down in steps of 1, at which every shown member's text fits its box less sidePadding on each side. Under min the
    // group goes on down to floor and reports it (Fits false) rather than wrap, cut or end in "…". The members never wrap
    // or autosize; their ThemedText leaves the size to the group (GroupOwned). Fitted again when enabled, when the group's
    // box resizes, when a member's text, box or visibility changed (TMP's text changed event; before each canvas draw for a
    // member hidden or shown, which TMP does not report) and when a member's font did. A row (ConfigureRow: a footer's
    // links and its version) sizes each member's box to its text: it fits when all of its shown children, the members'
    // texts at the size, its spacing and padding fit the row's own width, and it is laid out again after a refit, so no
    // box is squeezed under its text; a row told to keep clear of a text (KeepClearOf: the settings card's title, when
    // its footer shares the title's row) also keeps the children before its first flexible one left of that text and
    // the ones after it right of it, spacing apart. A group of a few lines (ConfigureLines: a message) wraps its members'
    // whole words on at most that many lines inside their boxes.
    [ExecuteAlways]
    public sealed class KitTextGroup : MonoBehaviour
    {
        [SerializeField] private float max = 44f;
        [SerializeField] private float min = 34f;
        [SerializeField] private float floor = 18f;
        [SerializeField] private float sidePadding;
        [SerializeField] private TMP_Text[] members = Array.Empty<TMP_Text>();
        // The members are children of this object's HorizontalLayoutGroup, each as wide as its text
        [SerializeField] private bool row;
        // The most lines a member's text may wrap onto (1: never wraps)
        [SerializeField] private int lines = 1;
        // The text a row keeps clear of, centred where its box is (set at runtime by the row's owner)
        private TMP_Text keepClear;

        private readonly List<ThemedText> followed = new List<ThemedText>();
        // What each member showed at the last fit: TMP's event fires on every regeneration, the group's own size change too
        private string[] seenTexts = Array.Empty<string>();
        private float[] seenWidths = Array.Empty<float>();
        private bool[] seenShown = Array.Empty<bool>();
        private float size;
        private bool fits = true;
        private bool fitting;
        private bool listening;
        // Refitted inside a graphic rebuild, where TMP does not take new dirty marks: redraw the members once it is over
        private bool redraw;

        // The shared size after the last fit
        public float Size => size;
        // False when the last fit went under min
        public bool Fits => fits;

        public void Configure(float max, float min, float floor, float sidePadding, params TMP_Text[] members) =>
            Configure(max, min, floor, sidePadding, false, 1, members);

        // Members whose whole words wrap onto at most lines lines (2 or more) inside their boxes
        public void ConfigureLines(float max, float min, float floor, int lines, params TMP_Text[] members)
        {
            if (lines < 2) throw new ArgumentOutOfRangeException(nameof(lines), "a group of one line is Configure");
            Configure(max, min, floor, 0f, false, lines, members);
        }

        // A row keeps clear of text (null: of nothing); fitted again when it changes
        public void KeepClearOf(TMP_Text text)
        {
            if (keepClear == text) return;
            keepClear = text;
            Fit();
        }

        // Members laid out in this object's HorizontalLayoutGroup, among its children, each box as wide as its text
        public void ConfigureRow(float max, float min, float floor, params TMP_Text[] members)
        {
            if (GetComponent<HorizontalLayoutGroup>() == null) throw new InvalidOperationException("a row needs a HorizontalLayoutGroup");
            foreach (var member in members ?? Array.Empty<TMP_Text>())
                if (member != null && member.transform.parent != transform) throw new ArgumentException("a row's members are its children", nameof(members));
            Configure(max, min, floor, 0f, true, 1, members);
        }

        private void Configure(float max, float min, float floor, float sidePadding, bool row, int lines, TMP_Text[] members)
        {
            if (!(floor > 0f && floor <= min && min <= max)) throw new ArgumentOutOfRangeException(nameof(floor), "0 < floor <= min <= max");
            if (sidePadding < 0f) throw new ArgumentOutOfRangeException(nameof(sidePadding));
            members ??= Array.Empty<TMP_Text>();
            foreach (var member in members)
                if (member == null) throw new ArgumentNullException(nameof(members));
            this.max = max;
            this.min = min;
            this.floor = floor;
            this.sidePadding = sidePadding;
            this.row = row;
            this.lines = lines;
            this.members = (TMP_Text[])members.Clone();
            foreach (var member in this.members)
            {
                member.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                member.enableAutoSizing = false;
                member.overflowMode = TextOverflowModes.Overflow;
                var themed = member.GetComponent<ThemedText>();
                if (themed != null) themed.GroupOwned = true;
            }
            if (listening) Follow();
            Fit();
        }

        private void OnEnable()
        {
            listening = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Canvas.willRenderCanvases += Redraw;
            Follow();
            Fit();
        }

        private void OnDisable()
        {
            listening = false;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            Canvas.willRenderCanvases -= Redraw;
            Unfollow();
        }

        private void OnRectTransformDimensionsChange() => Fit();

        // Recomputes the shared size now. Members not laid out yet (no width) leave the size as it is: their first layout
        // regenerates them, and the changed width fits the group then
        public void Fit()
        {
            if (fitting) return;
            fitting = true;
            try
            {
                if (LaidOut())
                {
                    var value = max;
                    var ok = AllFit(value);
                    while (!ok && value > floor)
                    {
                        value = Next(value);
                        ok = AllFit(value);
                    }
                    size = value;
                    fits = ok && value >= min;
                    foreach (var member in members)
                        if (member != null) member.fontSize = size;
                    if (CanvasUpdateRegistry.IsRebuildingGraphics() || (row && CanvasUpdateRegistry.IsRebuildingLayout())) redraw = true;
                    // A row's boxes follow its texts at once, or after the canvas update under way (Redraw)
                    else if (row) LayoutRow();
                }
                See();
            }
            finally
            {
                fitting = false;
            }
        }

        // One step down from value: 1 less, but min itself on the way and never under floor
        private float Next(float value)
        {
            var next = value - 1f;
            if (value > min && next < min) next = min;
            return Mathf.Max(next, floor);
        }

        private static bool Shown(TMP_Text member) => member != null && member.enabled && member.gameObject.activeInHierarchy;

        private bool LaidOut()
        {
            if (row) return ((RectTransform)transform).rect.width > 0f;
            foreach (var member in members)
                if (Shown(member) && member.rectTransform.rect.width <= 0f) return false;
            return true;
        }

        // Every shown member's text on one line at value fits its box less the side padding
        private bool AllFit(float value)
        {
            if (row) return RowFits(value);
            foreach (var member in members)
            {
                if (!Shown(member)) continue;
                member.fontSize = value;
                if (lines > 1)
                {
                    if (!FitsLines(member)) return false;
                    continue;
                }
                var width = member.GetPreferredValues(member.text ?? string.Empty).x;
                if (width > member.rectTransform.rect.width - 2f * sidePadding + 0.01f) return false;
            }
            return true;
        }

        // A member's whole words on at most lines lines inside its box less the side padding: its text no taller than the
        // box nor than lines and half another of its font's lines (a line more would be)
        private bool FitsLines(TMP_Text member)
        {
            var box = member.rectTransform.rect;
            var room = box.width - 2f * sidePadding;
            var text = member.GetPreferredValues(member.text ?? string.Empty, room, float.PositiveInfinity);
            var face = member.font != null ? member.font.faceInfo : default;
            var line = face.pointSize > 0f
                ? face.lineHeight * face.scale * member.fontSize / face.pointSize + member.lineSpacing * 0.01f * member.fontSize
                : member.fontSize * 1.2f;
            return text.x <= room + 0.01f && text.y <= Mathf.Min(box.height, (lines + 0.5f) * line) + 0.01f;
        }

        // The row's shown children (as its HorizontalLayoutGroup counts them) side by side at their preferred widths, the
        // members' texts at value, fit its width; kept clear of keepClear, the ones before the first flexible child end
        // left of it and the ones after that start right of it
        private bool RowFits(float value)
        {
            var layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null) return true;
            var widths = new List<float>();
            var split = -1;
            foreach (Transform child in transform)
            {
                if (!(child is RectTransform rect) || !child.gameObject.activeInHierarchy || Ignored(child)) continue;
                var member = child.GetComponent<TMP_Text>();
                if (member != null && Array.IndexOf(members, member) >= 0 && Shown(member))
                {
                    member.fontSize = value;
                    widths.Add(member.GetPreferredValues(member.text ?? string.Empty).x);
                }
                else
                {
                    if (split < 0 && LayoutUtility.GetFlexibleWidth(rect) > 0f) split = widths.Count;
                    widths.Add(LayoutUtility.GetPreferredWidth(rect));
                }
            }
            var box = ((RectTransform)transform).rect;
            var spacing = layout.spacing;
            var total = layout.padding.horizontal + spacing * Mathf.Max(0, widths.Count - 1);
            foreach (var width in widths) total += width;
            if (total > box.width + 0.01f) return false;
            if (keepClear == null || !keepClear.isActiveAndEnabled || split < 0) return true;
            var clear = keepClear.GetPreferredValues(keepClear.text ?? string.Empty).x / 2f + spacing;
            var centre = transform.InverseTransformPoint(keepClear.rectTransform.TransformPoint(keepClear.rectTransform.rect.center)).x;
            var left = box.xMin + layout.padding.left;
            for (var i = 0; i < split; i++) left += widths[i] + (i > 0 ? spacing : 0f);
            var right = box.xMax - layout.padding.right;
            for (var i = widths.Count - 1; i > split; i--) right -= widths[i] + (i < widths.Count - 1 ? spacing : 0f);
            return left <= centre - clear + 0.01f && right >= centre + clear - 0.01f;
        }

        // As LayoutGroup counts it: it has ignorers and every one of them ignores the layout
        private static bool Ignored(Transform child)
        {
            var ignorers = child.GetComponents<ILayoutIgnorer>();
            if (ignorers.Length == 0) return false;
            foreach (var ignorer in ignorers)
                if (!ignorer.ignoreLayout) return false;
            return true;
        }

        private void LayoutRow()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            See();
        }

        private void See()
        {
            var count = members.Length;
            if (seenTexts.Length != count)
            {
                seenTexts = new string[count];
                seenWidths = new float[count];
                seenShown = new bool[count];
            }
            for (var i = 0; i < count; i++)
            {
                var member = members[i];
                seenTexts[i] = member != null ? member.text : null;
                seenWidths[i] = member != null ? member.rectTransform.rect.width : 0f;
                seenShown[i] = Shown(member);
            }
        }

        private bool Changed()
        {
            if (seenTexts.Length != members.Length) return true;
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null) continue;
                if (seenShown[i] != Shown(member) || !string.Equals(seenTexts[i], member.text, StringComparison.Ordinal) ||
                    Mathf.Abs(seenWidths[i] - member.rectTransform.rect.width) > 0.01f) return true;
            }
            return false;
        }

        private void OnTextChanged(Object changed)
        {
            if (fitting || !(changed is TMP_Text text)) return;
            if (text == keepClear)
            {
                Fit();
                return;
            }
            if (Array.IndexOf(members, text) < 0) return;
            if (Changed()) Fit();
        }

        private void Redraw()
        {
            if (!fitting && Changed()) Fit();
            if (!redraw) return;
            redraw = false;
            if (row) LayoutRow();
            foreach (var member in members)
                if (Shown(member)) member.ForceMeshUpdate();
        }

        private void Follow()
        {
            Unfollow();
            foreach (var member in members)
            {
                var themed = member != null ? member.GetComponent<ThemedText>() : null;
                if (themed == null) continue;
                themed.FontChanged += Fit;
                followed.Add(themed);
            }
        }

        private void Unfollow()
        {
            foreach (var themed in followed)
                if (themed != null) themed.FontChanged -= Fit;
            followed.Clear();
        }
    }
}
