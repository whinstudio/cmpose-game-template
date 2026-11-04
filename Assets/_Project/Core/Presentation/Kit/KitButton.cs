using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A themed button (KitFactory.Button builds it): rounded to the style's radius, standing on the style's surface,
    // coloured by its variant; outside the clean family (KitShape.Chunky; not in a game whose skin dresses the kit) a
    // raised pill instead, its light label outlined in its body's colour, darker (LabelOutline; not on Secondary), and
    // pressed down onto its raised edge while the finger is down on it (Pressed: like a Selectable's pressed state, it
    // lifts while the finger slides off and presses again when that finger slides back). The Button, PressFeedback and
    // the Label child sit on the same object. An icon button (KitFactory.Button with a sprite) shows the glyph in a
    // faint disc at its left, the label beside it. A game skin (GameSkin) draws the body with its sprite instead, the
    // pressed sprite while the finger is down; the label and the glyph sit on the middle of the nine-slice centre of
    // the sprite that shows (ContentOffset), not on the middle of the whole sprite. Skinned or not, the label also
    // moves by its font's CapOffset, so its capitals sit on that middle rather than the font's ascender-to-descender
    // box. Danger (a destructive action) and Close (a card's round X) are red in every family; a skin draws them with
    // its Primary and Icon sprites (GameSkin.Button). A game's display font (a handwriting or pixel face) is hard to
    // read on a button: the label takes the body font, bold (LabelFont), but on Play.
    [RequireComponent(typeof(Button))]
    public sealed class KitButton : KitElement, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IPointerEnterHandler
    {
        [SerializeField] private KitButtonVariant variant;
        [SerializeField] private Image shadow;
        [SerializeField] private Image edge;
        [SerializeField] private Image body;
        [SerializeField] private Image shine;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image iconBack;
        [SerializeField] private Image icon;
        // How far the label and the glyph are moved off their places now (units); kept with them when a scene is saved
        [SerializeField] private Vector2 contentOffset;
        // How far the label alone is raised on top of that for its font's capitals now (CapOffset; units)
        [SerializeField] private float capOffset;
        // The skin entry the body is dressed with (null: the kit's own shape)
        private SkinButton skin;
        // The label's ThemedText whose font changes Recap hears
        private ThemedText followed;
        // The raised edge's depth the last palette Apply drew on a chunky button (units; 0: no pressed edge: clean, a skin)
        private float edgeDepth;
        // How far the body, the shine, the label and the glyph are moved down now for a press (units). Not serialized: a
        // press ends with the finger's up, OnDisable or the next Apply, which puts them back before anything else moves
        private float pressDepth;
        // Whether the skin's pressed sprite shows
        private bool skinPressed;
        // Watching for the button turning uninteractable while pressed (Watch); null while up
        private Coroutine watch;

        // The disc behind an icon: the glyph's colour, faint
        public const float IconBackOpacity = 0.14f;
        // A chunky label's outline (Primary, Play, Danger): its body's colour this much darker, this wide (the SDF
        // shader's _OutlineWidth), no underlay
        public const float LabelOutlineShade = 0.45f;
        public const float LabelOutlineWidth = 0.12f;

        public KitButtonVariant Variant => variant;
        public Image Body => body;
        public TMP_Text Label => label;
        // The glyph of an icon button (null without one)
        public Image Icon => icon;
        public Button Button => GetComponent<Button>();
        // The body's corner radius in units
        public float Radius => KitShape.RadiusOf(body);
        // Where the label and the glyph stand off the body's middle: on the middle of the nine-slice centre of the sprite
        // that shows (SkinButton.Lift); zero without a skin
        public Vector2 ContentOffset => contentOffset;
        // Drawn pressed now: a chunky button sat down on its edge, or a skin's pressed sprite
        public bool Pressed => pressDepth > 0f || skinPressed;

        internal void Init(KitButtonVariant value, Image shadowImage, Image edgeImage, Image bodyImage, Image shineImage, TMP_Text labelText)
        {
            variant = value;
            shadow = shadowImage;
            edge = edgeImage;
            body = bodyImage;
            shine = shineImage;
            label = labelText;
        }

        internal void InitIcon(Image backImage, Image glyphImage)
        {
            iconBack = backImage;
            icon = glyphImage;
        }

        public void SetVariant(KitButtonVariant value)
        {
            variant = value;
            if (Theme != null && Theme.Current != null) Apply(Theme.Current, Theme.Style);
        }

        public static float RadiusFor(KitButtonVariant variant, float styleRadius) => RadiusFor(variant, styleRadius, false);

        // The round variants (Icon, Close): a circle with one glyph or character on its middle
        public static bool IsRound(KitButtonVariant variant) => variant == KitButtonVariant.Icon || variant == KitButtonVariant.Close;

        // Icon and Close are always round; Play is rounder than the style's boxes; in a chunky family (KitShape.Chunky)
        // every variant is a pill
        public static float RadiusFor(KitButtonVariant variant, float styleRadius, bool chunky)
        {
            if (chunky || IsRound(variant)) return KitSprites.Pill;
            return variant == KitButtonVariant.Play ? styleRadius * 1.6f : styleRadius;
        }

        public static ThemeRole BodyRole(KitButtonVariant variant) => BodyRole(variant, false);

        // A chunky Secondary (KitShape.Chunky) stands in SurfaceVariant (Text on it reads at 4.5:1 in every family,
        // ThemeFamilyTests), not white Surface with a grey outline. Danger and Close are red in every family (OnPrimary on
        // Danger reads at 4.5:1, ThemeFamilyTests)
        public static ThemeRole BodyRole(KitButtonVariant variant, bool chunky)
        {
            switch (variant)
            {
                case KitButtonVariant.Secondary: return chunky ? ThemeRole.SurfaceVariant : ThemeRole.Surface;
                case KitButtonVariant.Play: return ThemeRole.Accent;
                case KitButtonVariant.Danger:
                case KitButtonVariant.Close: return ThemeRole.Danger;
                default: return ThemeRole.Primary;
            }
        }

        // The label's font: a game's display font (a handwriting or pixel face) is the Play button's alone; the other
        // labels take the body font, bold
        public static TextRole LabelFont(KitButtonVariant variant) =>
            variant == KitButtonVariant.Play ? TextRole.Title : TextRole.Body;

        // The glyph's colour: the Secondary button's in Primary (a large shape: Primary on its Surface body, or on the
        // SurfaceVariant body of a chunky family, reads at 3:1 in every family, ThemeFamilyTests), the others' in their
        // label colour
        public static ThemeRole IconRole(KitButtonVariant variant) =>
            variant == KitButtonVariant.Secondary ? ThemeRole.Primary : LabelRole(variant);

        // OnPrimary on Primary, Icon, Danger and Close
        public static ThemeRole LabelRole(KitButtonVariant variant)
        {
            switch (variant)
            {
                case KitButtonVariant.Secondary: return ThemeRole.Text;
                case KitButtonVariant.Play: return ThemeRole.OnAccent;
                default: return ThemeRole.OnPrimary;
            }
        }

        // Whether a chunky label is outlined: the light labels (on Primary, Play, Danger); Secondary's dark text on its
        // light body is not, nor Icon's and Close's glyphs
        public static bool LabelOutline(KitButtonVariant variant) =>
            variant == KitButtonVariant.Primary || variant == KitButtonVariant.Play || variant == KitButtonVariant.Danger;

        public void OnPointerDown(PointerEventData eventData) => Down();

        // uGUI sends the up to the pressed object whatever its state now (moved off, made uninteractable)
        public void OnPointerUp(PointerEventData eventData) => Release();

        // The finger slid off: drawn up until it comes back (the up may follow off the button: it stays up)
        public void OnPointerExit(PointerEventData eventData) => Release();

        // Back on with the finger that pressed it (uGUI keeps the pressed object until the up); another finger or a mouse
        // passing over it does not press it
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerPress == gameObject) Down();
        }

        private void OnDisable()
        {
            Release();
            if (watch != null) StopCoroutine(watch);
            watch = null;
        }

        private void Down()
        {
            if (body == null || !Button.IsInteractable()) return;
            if (skin == null)
            {
                Press(edgeDepth);
                Watch();
                return;
            }
            skinPressed = true;
            body.sprite = skin.Pressed;
            Offset(FaceOffset(skin.Pressed));
        }

        // Made uninteractable while the finger is down: up at once, not on the finger's up. Checked once a frame only while
        // the edge is pressed down, so the buttons that are up cost nothing per frame (edit mode runs no frames)
        private void Watch()
        {
            if (watch != null || pressDepth <= 0f || !Application.isPlaying || !isActiveAndEnabled) return;
            watch = StartCoroutine(WatchInteractable());
        }

        private IEnumerator WatchInteractable()
        {
            while (pressDepth > 0f)
            {
                yield return null;
                if (pressDepth > 0f && !Button.IsInteractable()) Release();
            }
            watch = null;
        }

        private void Release()
        {
            Press(0f);
            if (!skinPressed) return;
            skinPressed = false;
            if (skin == null || body == null) return;
            body.sprite = skin.Normal;
            Offset(FaceOffset(skin.Normal));
        }

        // Moves the body, the shine, the label and the glyph (the disc with its glyph) down to depth units below their
        // places; the edge stays, so at the edge's depth it no longer shows. Moves by the difference, like Offset, so the
        // press composes with the content's offset and its capitals' and comes back exactly
        private void Press(float depth)
        {
            var delta = depth - pressDepth;
            if (delta == 0f) return;
            pressDepth = depth;
            var move = new Vector2(0f, -delta);
            if (body != null) body.rectTransform.anchoredPosition += move;
            if (shine != null) shine.rectTransform.anchoredPosition += move;
            if (label != null) label.rectTransform.anchoredPosition += move;
            var glyph = iconBack != null ? iconBack : icon;
            if (glyph != null) glyph.rectTransform.anchoredPosition += move;
        }

        // How far the label's capitals sit below its middle (units; negative: above) at the size TMP draws it (fontSize:
        // the size autosizing chose once laid out): TMP's Center puts the middle of the font's ascender-to-descender box
        // on the box's middle, the capitals span baseline to cap line
        public static float CapOffset(TMP_Text label)
        {
            var font = label != null ? label.font : null;
            if (font == null) return 0f;
            var face = font.faceInfo;
            if (face.pointSize <= 0) return 0f;
            return ((face.ascentLine + face.descentLine) / 2f - face.capLine / 2f) * label.fontSize / face.pointSize * face.scale;
        }

        // The label's ThemedText may set its font after Apply (bound alone): the capitals follow it
        private void Follow(ThemedText text)
        {
            if (text == followed) return;
            if (followed != null) followed.FontChanged -= Recap;
            followed = text;
            if (followed != null) followed.FontChanged += Recap;
        }

        // TMP chooses an autosized label's size while laying it out: the capitals follow that size once it is known.
        // Moving the label does not lay it out again, so this does not loop
        private void FollowLayout()
        {
            if (label == null) return;
            label.OnPreRenderText -= LaidOut;
            label.OnPreRenderText += LaidOut;
        }

        private void LaidOut(TMP_TextInfo _) => Recap();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (label != null) label.OnPreRenderText -= LaidOut;
            Follow(null);
        }

        // Raises the label by its font's CapOffset, on top of the face's offset
        private void Recap()
        {
            if (!this || label == null) return;
            var value = CapOffset(label);
            var delta = value - capOffset;
            if (delta == 0f) return;
            capOffset = value;
            label.rectTransform.anchoredPosition += new Vector2(0f, delta);
        }

        // The skin's lift of sprite in units: lift source pixels drawn at the skin's SliceScale
        private Vector2 FaceOffset(Sprite sprite)
        {
            var current = GameSkin.Current;
            if (skin == null || sprite == null || current == null) return Vector2.zero;
            var canvas = GetComponentInParent<Canvas>(true);
            var reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            return new Vector2(0f, skin.Lift(sprite) * current.SliceScale * reference / sprite.pixelsPerUnit);
        }

        // Moves the label and the glyph (the disc with its glyph, if there is one) to value off their places
        private void Offset(Vector2 value)
        {
            var delta = value - contentOffset;
            if (delta == Vector2.zero) return;
            contentOffset = value;
            if (label != null) label.rectTransform.anchoredPosition += delta;
            var glyph = iconBack != null ? iconBack : icon;
            if (glyph != null) glyph.rectTransform.anchoredPosition += delta;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            // Back up first: Layers sets the shine's rect afresh, and offsets must never add up
            Release();
            edgeDepth = 0f;
            var labelFont = label != null ? label.GetComponent<ThemedText>() : null;
            // The font first: the capitals below follow the font the label ends up with
            SetLabelFont(labelFont);
            Follow(labelFont);
            FollowLayout();
            Recap();
            var current = GameSkin.Current;
            skin = current?.Button(variant);
            if (skin != null)
            {
                if (label != null) KitTextOutline.Clear(label);
                Dress(current);
                return;
            }
            Offset(Vector2.zero);
            // A chunky family's buttons are raised pills whatever its surface; a raised base, no outline
            var chunky = KitShape.Chunky(style);
            var radius = RadiusFor(variant, style != null ? style.CornerRadius : 0f, chunky);
            ThemeRole? outline = !chunky && variant == KitButtonVariant.Secondary ? ThemeRole.Control : (ThemeRole?)null;
            var bodyRole = BodyRole(variant, chunky);
            KitShape.Layers(shadow, edge, body, shine, radius, style, bodyRole, outline,
                surface: chunky ? SurfaceStyle.Raised : (SurfaceStyle?)null);
            if (chunky && edge != null) edgeDepth = KitShape.RaisedDepth(style);
            if (label != null)
            {
                var themed = Themed(label);
                themed.SetFixed(null);
                if (themed.Role != LabelRole(variant)) themed.SetRole(LabelRole(variant));
                if (chunky && LabelOutline(variant))
                    KitTextOutline.Apply(label, ThemedGraphic.Tone(palette.Get(bodyRole), LabelOutlineShade, 1f), LabelOutlineWidth, false);
                else KitTextOutline.Clear(label);
            }
            if (icon == null) return;
            var role = IconRole(variant);
            var glyph = Themed(icon);
            glyph.SetFixed(null);
            if (glyph.Role != role) glyph.SetRole(role);
            if (iconBack == null) return;
            var back = Themed(iconBack);
            back.SetFixed(null);
            if (back.Role != role) back.SetRole(role);
            back.SetTone(0f, IconBackOpacity);
        }

        // The label in LabelFont, bold in the body font, skinned or not; bound again so the font follows the role at once
        private void SetLabelFont(ThemedText text)
        {
            if (text == null) return;
            var role = LabelFont(variant);
            if (role == TextRole.Body) text.SetBold();
            if (text.Role == role) return;
            text.SetRole(role);
            text.Bind(Theme);
        }

        // The skin's sprite in its own colours; the label and the glyph in the skin's text colour
        private void Dress(GameSkin current)
        {
            KitShape.Dress(shadow, edge, body, shine, skin.Normal, current.SliceScale, current.Pixel);
            if (label != null) KitShape.Fix(label, skin.Text);
            if (icon != null) KitShape.Fix(icon, skin.Text);
            if (iconBack != null) KitShape.Fix(iconBack, ThemedGraphic.Tone(skin.Text, 0f, IconBackOpacity));
            Offset(FaceOffset(skin.Normal));
        }
    }
}
