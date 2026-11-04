using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A rounded panel on the style's surface (KitFactory.Card builds it): Surface, or SurfaceVariant when emphasised.
    // Content goes under the card after its Body child. A game skin (GameSkin) draws it with its card sprite, or as a
    // sheet's panel with the sheet's or the dialog's. A padded card (Pad) keeps its content clear of that sprite's
    // frame: on a side where the frame is thicker than the padding, the content sits FrameGap inside its inner edge.
    // A sheet's panel in a chunky family (KitShape.Chunky: never under a skin that dresses the kit) is framed: a Primary frame
    // PaletteFrame units wide round the Surface panel, on a darker Primary base. On a sheet's panel only the titles and
    // the big numbers keep the display font (KeepsDisplayFont); its other texts take the body font. A framed card's
    // header (Header: its Title) sits on a band of the frame (Banded): the Surface panel starts BandGap below the
    // header's bottom edge, the header in OnPrimary with a dark Primary outline and an underlay (KitTextOutline); with
    // another text, control or picture on that band (the card's own Close X aside) the card stays as it was. The band is
    // placed on a paint and again after the canvas's layout (CanvasUpdate.PostLayout) once the card shows, resizes (a
    // rotation, the settings card's landscape row) or is painted: a column card's children are laid out after the card
    // shows and after it resizes, so the rects a paint or a resize sees may not be the ones drawn. ExecuteAlways so the
    // same happens in edit mode tests; an unbound card (a scene opened in the editor) does nothing.
    [ExecuteAlways]
    public sealed class KitCard : KitElement, ICanvasElement
    {
        // Units between a skin frame's inner edge and the content (3 source pixels of a pixel pack at sliceScale 4)
        public const float FrameGap = 12f;
        // Units of the frame round a framed card (Framed), on every side
        public const float PaletteFrame = 16f;
        // Units between a banded header's bottom edge and the Surface panel (Banded)
        public const float BandGap = 12f;
        // A banded header's outline: Primary this much darker, this wide (the SDF shader's _OutlineWidth), with an underlay
        public const float TitleOutlineShade = 0.45f;
        public const float TitleOutlineWidth = 0.2f;

        [SerializeField] private bool emphasis;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;
        // Pad's padding (without a skin's frame) and the content it pads besides the card's own LayoutGroup
        [SerializeField] private bool padded;
        [SerializeField] private RectOffset padding = new RectOffset();
        // Pad's cap on the frame thickness the padding makes room for (0: none)
        [SerializeField] private float frameLimit;
        [SerializeField] private RectTransform content;
        // The header on the band now and the colour role (or, without a ThemedGraphic, the colour) it was built with,
        // given back when the band goes (kept with them when a scene is saved, so a reload still knows them)
        [SerializeField] private TMP_Text bandHeader;
        [SerializeField] private ThemeRole headerRole;
        [SerializeField] private Color headerColour;
        // Placing the band sets rects and a role: one placing at a time
        private bool placing;
        // Clear's and LocalRect's buffers, shared by every card: the band is placed after every layout of a card, on the
        // main thread, one card at a time, and nothing it calls places another card's band
        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>();
        private static readonly List<Selectable> SelectableBuffer = new List<Selectable>();
        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        public Image Body => body;
        public bool Emphasis => emphasis;
        public float Radius => KitShape.RadiusOf(body);
        // Drawn framed: a sheet's panel in a chunky family (KitShape.Chunky; a skin that dresses the kit draws it with
        // its sprite instead)
        public bool Framed => FramedIn(Theme != null ? Theme.Style : null);
        // The card's header: on a sheet's panel, the drawn (active, not faded out) ThemedText named Title right under the
        // sheet's Content, or under the card itself (a column card such as settings); null without one
        public Transform Header
        {
            get
            {
                var header = HeaderText();
                return header != null ? header.transform : null;
            }
        }
        // The header sits on a band of the Primary frame
        public bool Banded => bandHeader != null;

        internal void Init(bool emphasised, Image shadowImage, Image edgeImage, Image bodyImage)
        {
            emphasis = emphasised;
            shadow = shadowImage;
            edge = edgeImage;
            body = bodyImage;
        }

        // The padding Pad was given (zeros for an unpadded card)
        public RectOffset Padding => new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        // The frame thickness Pad was told to make room for at most (0: no limit)
        public float FrameLimit => frameLimit;

        // The padding the content gets: Padding, or on each side the skin frame's thickness (at most Pad's frameLimit) plus
        // FrameGap where that is more. A frame without a border (or no skin) leaves Padding as it is.
        public RectOffset ContentPadding
        {
            get
            {
                var frame = Frame;
                return new RectOffset(Side(padding.left, frame.x, frameLimit), Side(padding.right, frame.z, frameLimit),
                    Side(padding.top, frame.w, frameLimit), Side(padding.bottom, frame.y, frameLimit));
            }
        }

        // How far the frame reaches into the card on each side, in the card's units (left, bottom, right, top): a skin
        // sprite's 9-slice border drawn at the skin's SliceScale, PaletteFrame when Framed; else zero
        public Vector4 Frame
        {
            get
            {
                var skin = GameSkin.Current;
                var sprite = skin != null ? SkinSprite(skin) : null;
                if (sprite == null) return Framed ? Vector4.one * PaletteFrame : Vector4.zero;
                var canvas = GetComponentInParent<Canvas>(true);
                var reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
                return sprite.border * (reference * skin.SliceScale / sprite.pixelsPerUnit);
            }
        }

        // Pads the card's content: the LayoutGroup on the card (a column card) and content (a sheet's Content), if any.
        // Applied at once and again whenever the card is painted. A later call replaces the padding and keeps the content
        // an earlier one gave unless it passes another (the settings column re-pads its sheet's card: the unused Content
        // then follows the column's padding too). A frameLimit over 0 lets the content reach over a frame thicker than that
        // (the settings card keeps its controls touch-sized on a landscape phone inside a thick frame).
        public void Pad(RectOffset value, RectTransform contentRect = null, float frameLimit = 0f)
        {
            if (frameLimit < 0f) throw new System.ArgumentOutOfRangeException(nameof(frameLimit));
            padded = true;
            padding = new RectOffset(value.left, value.right, value.top, value.bottom);
            this.frameLimit = frameLimit;
            if (contentRect != null) content = contentRect;
            Inset();
        }

        // Whole units, rounded up (a hair over a whole number is float noise, not another unit)
        private static int Side(int padding, float frame, float limit)
        {
            if (limit > 0f) frame = Mathf.Min(frame, limit);
            return frame > 0f ? Mathf.Max(padding, Mathf.CeilToInt(frame + FrameGap - 0.01f)) : padding;
        }

        private void Inset()
        {
            if (!padded) return;
            var inset = ContentPadding;
            var layout = GetComponent<LayoutGroup>();
            if (layout != null && !Same(layout.padding, inset)) layout.padding = inset;
            if (content == null) return;
            content.offsetMin = new Vector2(inset.left, inset.bottom);
            content.offsetMax = new Vector2(-inset.right, -inset.top);
        }

        private static bool Same(RectOffset a, RectOffset b) =>
            a != null && a.left == b.left && a.right == b.right && a.top == b.top && a.bottom == b.bottom;

        // Whether text keeps the display font on a sheet's panel: a title (named Title or Subtitle) or a Display text (a
        // big number)
        public static bool KeepsDisplayFont(Transform text)
        {
            if (text == null) return false;
            if (text.name == "Title" || text.name == "Subtitle") return true;
            var themed = text.GetComponent<ThemedText>();
            return themed != null && themed.Role == TextRole.Display;
        }

        public void SetEmphasis(bool value)
        {
            emphasis = value;
            if (Theme != null && Theme.Current != null) Apply(Theme.Current, Theme.Style);
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            AfterLayout();
            Inset();
            OneFont();
            if (body == null) return;
            var skin = GameSkin.Current;
            var sprite = skin != null ? SkinSprite(skin) : null;
            // The body fills the card unless it is framed
            if (sprite != null)
            {
                Unband();
                KitShape.Offsets(body, 0f, 0f, 0f, 0f);
                KitShape.Dress(shadow, edge, body, null, sprite, skin.SliceScale, skin.Pixel);
                return;
            }
            // Panels are a step rounder than the buttons on them
            var radius = (style != null ? style.CornerRadius : 0f) * 1.25f;
            var role = emphasis ? ThemeRole.SurfaceVariant : ThemeRole.Surface;
            if (FramedIn(style))
            {
                DrawFrame(radius, role, style, palette);
                return;
            }
            Unband();
            KitShape.Offsets(body, 0f, 0f, 0f, 0f);
            KitShape.Layers(shadow, edge, body, null, radius, style, role, depthScale: 0.6f);
        }

        // The framed look on the three layers: Edge is the frame (Primary, the card's whole rect), Body the panel
        // PaletteFrame inside it (below the band at the top, PlaceBand), Shadow the frame's darker base, dropped as far as a
        // raised button's
        private void DrawFrame(float radius, ThemeRole bodyRole, ThemeStyle style, Palette palette)
        {
            KitShape.Round(body, Mathf.Max(radius - PaletteFrame, 8f));
            PlaceBand(palette);
            KitShape.SetRole(body, bodyRole, 0f, 1f);
            if (edge != null)
            {
                edge.gameObject.SetActive(true);
                KitShape.Round(edge, radius);
                KitShape.Offsets(edge, 0f, 0f, 0f, 0f);
                KitShape.SetRole(edge, ThemeRole.Primary, 0f, 1f);
            }
            if (shadow != null)
            {
                var depth = KitShape.RaisedDepth(style);
                shadow.gameObject.SetActive(true);
                KitShape.Round(shadow, radius);
                KitShape.Offsets(shadow, 0f, -depth, 0f, -depth);
                KitShape.SetRole(shadow, ThemeRole.Primary, 0.35f, 1f);
            }
        }

        // A skin that dresses the kit has a sheet and a dialog sprite and is never chunky (KitShape.Chunky)
        private bool FramedIn(ThemeStyle style) => KitShape.Chunky(style) && SheetOf() != null;

        // Places the band for the card's rects now and again after the next layout: call it when a part on or by the band
        // shows, hides or fades without the card resizing (the settings card's language page)
        public void RefreshBand()
        {
            PlaceBandNow();
            AfterLayout();
        }

        private void PlaceBandNow()
        {
            if (body == null || Theme == null || Theme.Current == null) return;
            var skin = GameSkin.Current;
            if ((skin == null || SkinSprite(skin) == null) && FramedIn(Theme.Style)) PlaceBand(Theme.Current);
            else Unband();
        }

        // The band placed again after the canvas's next layout (PostLayout; a layout running now still reaches it: the
        // registry runs what joins its queue during the pass). Only a bound card: an unbound one draws nothing of its own
        private void AfterLayout()
        {
            if (Theme == null) return;
            CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(this);
        }

        // The card's ContentSizeFitter resizes it before its VerticalLayoutGroup places the children: the band waits for
        // the layout
        private void OnRectTransformDimensionsChange() => AfterLayout();

        // A sheet's card shows before its column is laid out
        private void OnEnable() => AfterLayout();

        public void Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.PostLayout && isActiveAndEnabled) PlaceBandNow();
        }

        public void LayoutComplete()
        {
        }

        public void GraphicUpdateComplete()
        {
        }

        public bool IsDestroyed() => this == null;

        // The Surface panel PaletteFrame inside the frame, its top BandGap below the header's bottom edge when the header
        // can sit on a band; the header coloured and outlined for it, or given back as built
        private void PlaceBand(Palette palette)
        {
            if (placing) return;
            placing = true;
            try
            {
                var header = HeaderText();
                var top = -PaletteFrame;
                var banded = header != null && BandTop(header, out top);
                SetBody(banded ? top : -PaletteFrame);
                if (banded) Hold(header, palette);
                else Unband();
            }
            finally
            {
                placing = false;
            }
        }

        // The panel's top offset from the card's top under header's band: BandGap below the header's bottom edge, at least
        // the frame; false when that leaves no panel or another part would sit on the band
        private bool BandTop(TMP_Text header, out float top)
        {
            var card = (RectTransform)transform;
            var box = card.rect;
            top = Mathf.Min(LocalRect(card, header.rectTransform).yMin - BandGap - box.yMax, -PaletteFrame);
            if (box.yMax + top <= box.yMin + PaletteFrame) return false;
            return Clear(card, Rect.MinMaxRect(box.xMin, box.yMax + top, box.xMax, box.yMax), header.transform);
        }

        // Whether nothing drawn but the header and the card's Close X falls on band (in the card's units): no other text,
        // control or picture (a Primary icon would vanish into the frame). A control's own pictures (a switch knob's soft
        // shadow reaching past it) count by the control's rect; an invisible touch area or raycast surface draws nothing
        private bool Clear(RectTransform card, Rect band, Transform header)
        {
            try
            {
                GetComponentsInChildren(true, GraphicBuffer);
                foreach (var graphic in GraphicBuffer)
                {
                    if (graphic == body || graphic == edge || graphic == shadow || !graphic.enabled || DrawsNothing(graphic)) continue;
                    if (!(graphic is TMP_Text) && graphic.GetComponentInParent<Selectable>(true) != null) continue;
                    if (graphic.transform.IsChildOf(header) || InCloseX(graphic.transform) || !Drawn(graphic.transform)) continue;
                    if (Overlaps(band, LocalRect(card, graphic.rectTransform))) return false;
                }
                GetComponentsInChildren(true, SelectableBuffer);
                foreach (var selectable in SelectableBuffer)
                {
                    if (!selectable.enabled || InCloseX(selectable.transform) || !Drawn(selectable.transform)) continue;
                    if (Overlaps(band, LocalRect(card, (RectTransform)selectable.transform))) return false;
                }
                return true;
            }
            finally
            {
                // No card keeps another's parts alive
                GraphicBuffer.Clear();
                SelectableBuffer.Clear();
            }
        }

        // A touch area (clear) or a raycast surface; a text always counts (it may be faded in later)
        private static bool DrawsNothing(Graphic graphic)
        {
            if (graphic is TMP_Text) return false;
            if (graphic is RaycastSurface) return true;
            return graphic.TryGetComponent<ThemedGraphic>(out var themed) ? themed.Opacity <= 0f : graphic.color.a <= 0f;
        }

        // The card's own round X (it overhangs the corner, over the band)
        private bool InCloseX(Transform t)
        {
            var button = t.GetComponentInParent<KitButton>(true);
            return button != null && button.Variant == KitButtonVariant.Close && button.transform.IsChildOf(transform);
        }

        // Active and not faded out (a CanvasGroup at alpha 0: the settings page under its language page) from t up to the
        // card; the card's own CanvasGroup is the sheet's pop, not a veil
        private bool Drawn(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t.TryGetComponent<CanvasGroup>(out var group) && group.enabled && group.alpha <= 0f) return false;
            }
            return t == transform;
        }

        private static Rect LocalRect(RectTransform space, RectTransform rect)
        {
            rect.GetWorldCorners(CornerBuffer);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in CornerBuffer)
            {
                Vector2 local = space.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // More than half a unit in common on both axes (rects that touch do not overlap)
        private static bool Overlaps(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.5f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.5f;

        // The panel PaletteFrame inside the card's sides and bottom, top units from its top; only what changed (a scene
        // saved after a repaint keeps its YAML)
        private void SetBody(float top)
        {
            var rect = body.rectTransform;
            var offsetMin = new Vector2(PaletteFrame, PaletteFrame);
            var offsetMax = new Vector2(-PaletteFrame, top);
            if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
            if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
            if ((rect.offsetMin - offsetMin).sqrMagnitude > 1e-6f) rect.offsetMin = offsetMin;
            if ((rect.offsetMax - offsetMax).sqrMagnitude > 1e-6f) rect.offsetMax = offsetMax;
        }

        // The header on the band: OnPrimary (its role, or its colour without a ThemedGraphic), outlined; what it had is
        // kept for Unband
        private void Hold(TMP_Text header, Palette palette)
        {
            var themed = header.GetComponent<ThemedGraphic>();
            if (bandHeader != header)
            {
                Unband();
                headerRole = themed != null ? themed.Role : ThemeRole.Text;
                headerColour = header.color;
                bandHeader = header;
            }
            if (themed != null)
            {
                if (themed.Role != ThemeRole.OnPrimary) themed.SetRole(ThemeRole.OnPrimary);
            }
            else
            {
                var onPrimary = palette.Get(ThemeRole.OnPrimary);
                if (header.color != onPrimary) header.color = onPrimary;
            }
            KitTextOutline.Apply(header, ThemedGraphic.Tone(palette.Get(ThemeRole.Primary), TitleOutlineShade, 1f), TitleOutlineWidth, true);
        }

        // The band's header back as built: its role (or its colour), its font's own material
        private void Unband()
        {
            if (bandHeader != null)
            {
                KitTextOutline.Clear(bandHeader);
                var themed = bandHeader.GetComponent<ThemedGraphic>();
                if (themed != null)
                {
                    if (themed.Role != headerRole) themed.SetRole(headerRole);
                }
                else if (bandHeader.color != headerColour)
                {
                    bandHeader.color = headerColour;
                }
            }
            bandHeader = null;
        }

        private TMP_Text HeaderText()
        {
            var sheet = SheetOf();
            if (sheet == null) return null;
            return TitleUnder(sheet.Content) ?? TitleUnder(transform);
        }

        private TMP_Text TitleUnder(Transform parent)
        {
            if (parent == null || !parent.IsChildOf(transform)) return null;
            foreach (Transform child in parent)
            {
                if (child.name != "Title" || child.GetComponent<ThemedText>() == null) continue;
                var text = child.GetComponent<TMP_Text>();
                if (text != null && text.enabled && Drawn(child)) return text;
            }
            return null;
        }

        // One font on a sheet's panel: its Title texts but the titles (KeepsDisplayFont) take the body font, bold, so a
        // game's display face never sits beside the body font on one card. A button's texts are the button's (KitButton
        // gives its label LabelFont)
        private void OneFont()
        {
            if (SheetOf() == null) return;
            foreach (var text in GetComponentsInChildren<ThemedText>(true))
            {
                if (text.Role != TextRole.Title || KeepsDisplayFont(text.transform)) continue;
                var button = text.GetComponentInParent<KitButton>(true);
                if (button != null && button.transform.IsChildOf(transform)) continue;
                text.SetRole(TextRole.Body);
                text.SetBold();
                text.Bind(Theme);
            }
        }

        // The sheet whose panel this card is (null: a card of its own). SceneBuilder's modals move the panel into a safe
        // area under the sheet, so the sheet may be further up than the parent
        private KitSheet SheetOf()
        {
            var sheet = transform.parent != null ? transform.parent.GetComponentInParent<KitSheet>(true) : null;
            return sheet != null && sheet.Panel == transform ? sheet : null;
        }

        private Sprite SkinSprite(GameSkin skin)
        {
            var sheet = SheetOf();
            return sheet != null ? KitSheet.PanelSprite(skin, sheet.Placement) : skin.Card;
        }
    }
}
