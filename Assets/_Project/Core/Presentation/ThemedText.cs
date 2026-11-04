using System;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Sets the font of the TMP text on this object from its role and the theme's style; UiBinder binds it. A style
    // without that font leaves the text's own.
    // The kit's text boxes and sizes are drawn for line boxes (ascender to descender) of 1.2 to 1.36 em, the bundled
    // fonts' but Baloo 2's (1.6 em). A tall font (above TallLine) would spill out of them at the same size, so with one
    // the text may shrink to fit (TMP autosizing) down to the size at which its line box is KitLine: a fixed-size text
    // from its own size, an autosized one from its own largest size down to its own smallest by the same share. Only as
    // far as it needs: a flat scale would draw every tall-font text a fifth smaller. A font that is not tall gives the
    // text its own sizing back. Bound again to the font it has (every scene load binds the kept objects again) changes
    // nothing, so code may size a bound text; with a tall font a fixed-size text is autosized, so it sets fontSizeMax
    // too. The own sizing is serialized, so a bound copy (Instantiate: MenuView's game and side buttons) counts as
    // fitted and does not shrink once more from the shrunk sizes. A text in a KitTextGroup (GroupOwned) is sized by its
    // group: it only takes the font.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ThemedText : MonoBehaviour
    {
        // The line box in ems the kit's sizes count on, and the one above which a font is tall
        public const float KitLine = 1.3f;
        public const float TallLine = 1.45f;

        [SerializeField] private TextRole role = TextRole.Body;
        // Whether a tall font changed this text's sizing, and its own sizing from before
        [SerializeField, HideInInspector] private bool fitted;
        [SerializeField, HideInInspector] private bool ownAutoSize;
        [SerializeField, HideInInspector] private float ownSize;
        [SerializeField, HideInInspector] private float ownMin;
        [SerializeField, HideInInspector] private float ownMax;
        // Sized by a KitTextGroup: Bind leaves its sizing alone
        [SerializeField, HideInInspector] private bool groupOwned;

        public TextRole Role => role;

        public bool GroupOwned
        {
            get => groupOwned;
            set => groupOwned = value;
        }

        // Fires after Bind set another font or SetBold made the text bold (KitButton re-centres its label's capitals on it,
        // a KitTextGroup fits its members again)
        public event Action FontChanged;

        // Sets the role before binding (KitFactory); Bind applies it
        public void SetRole(TextRole value) => role = value;

        // Makes the text bold (KitButton's labels, KitCard's former titles): a bold face is wider, so to what sizes the
        // text by its font this is another font
        public void SetBold()
        {
            if (!this) return;
            var text = GetComponent<TMP_Text>();
            if ((text.fontStyle & FontStyles.Bold) != 0) return;
            text.fontStyle |= FontStyles.Bold;
            FontChanged?.Invoke();
        }

        public void Bind(IThemeSource source)
        {
            if (!this || source == null || source.Style == null) return;
            var font = source.Style.FontFor(role);
            if (font == null) return;
            var text = GetComponent<TMP_Text>();
            var tall = IsTall(font);
            if (text.font == font && (groupOwned || fitted == tall)) return;
            if (!groupOwned) Fit(text, tall ? KitLine / LineBox(font) : 1f);
            if (text.font == font) return;
            text.font = font;
            FontChanged?.Invoke();
        }

        // The font's line box (ascender to descender) in ems; 0 without a font
        public static float LineBox(TMP_FontAsset font)
        {
            if (font == null) return 0f;
            var face = font.faceInfo;
            return face.pointSize > 0 ? (face.ascentLine - face.descentLine) * face.scale / face.pointSize : 0f;
        }

        public static bool IsTall(TMP_FontAsset font) => LineBox(font) > TallLine;

        // share below 1 (a tall font): how far the text may shrink below its own size or minimum; 1 restores its own
        private void Fit(TMP_Text text, float share)
        {
            if (!fitted)
            {
                if (share >= 1f) return;
                ownAutoSize = text.enableAutoSizing;
                ownSize = text.fontSize;
                ownMin = text.fontSizeMin;
                ownMax = text.fontSizeMax;
                fitted = true;
            }
            if (share >= 1f)
            {
                text.enableAutoSizing = ownAutoSize;
                text.fontSizeMin = ownMin;
                text.fontSizeMax = ownMax;
                text.fontSize = ownSize;
                fitted = false;
                return;
            }
            var top = ownAutoSize ? ownMax : ownSize;
            text.fontSizeMax = top;
            text.fontSizeMin = Mathf.Min(ownAutoSize ? ownMin : ownSize, top) * share;
            text.enableAutoSizing = true;
        }
    }
}
