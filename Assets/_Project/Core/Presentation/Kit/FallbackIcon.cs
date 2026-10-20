using System.Globalization;
using Kturker.UnityBase.Core.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // An icon with a fallback: the glyph sprite when there is one, else a disc with the first letter of a name (a
    // booster or a currency without an icon, or a pack that did not download). A booster's disc is in the theme's
    // role; a currency's glyph or disc is in its own colour with a darker rim (KitGlyph), the letter in black or white,
    // whichever reads on it. ShowPlain leaves only the disc in its role (the HUD counter's dot without an economy).
    public sealed class FallbackIcon : MonoBehaviour
    {
        [SerializeField] private Image glyph;
        [SerializeField] private Image disc;
        [SerializeField] private LocalizedText letter;

        public Image Glyph => glyph;
        public Image Disc => disc;
        public LocalizedText Letter => letter;
        public bool UsesFallback { get; private set; }

        // name: the localized name the letter is taken from
        public void ShowName(Sprite sprite, string name)
        {
            Unpaint();
            if (!Apply(sprite)) return;
            letter.SetLiteral(LetterOf(name));
        }

        // nameKey: the name's string key (the letter follows the language the text is bound to)
        public void ShowKey(Sprite sprite, string nameKey)
        {
            Unpaint();
            if (!Apply(sprite)) return;
            letter.SetKey(nameKey);
            var text = letter.GetComponent<TMPro.TMP_Text>();
            letter.SetLiteral(LetterOf(text != null && text.text != nameKey ? text.text : null));
        }

        // A currency: its glyph in its colour, else a disc in its colour with its letter
        public void ShowCurrency(CurrencyMark mark)
        {
            if (mark == null)
            {
                ShowPlain();
                return;
            }
            if (!Apply(mark.Icon))
            {
                KitGlyph.Paint(glyph, mark.Colour);
                return;
            }
            KitGlyph.Paint(disc, mark.Colour);
            letter.SetLiteral(mark.Letter);
            var ink = mark.LetterColour;
            var themed = letter.GetComponent<ThemedGraphic>();
            if (themed != null) themed.SetFixed(ink);
            var text = letter.GetComponent<Graphic>();
            if (text != null) text.color = ink;
        }

        // Only the disc, in its role
        public void ShowPlain()
        {
            Unpaint();
            UsesFallback = true;
            glyph.enabled = false;
            if (disc != null) disc.gameObject.SetActive(true);
            letter.gameObject.SetActive(false);
        }

        // The first letter or digit of a name, upper case ("?" for none): the fallback's mark
        public static string LetterOf(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var c in name)
                    if (char.IsLetterOrDigit(c)) return char.ToUpper(c, CultureInfo.InvariantCulture).ToString();
            }
            return "?";
        }

        // True when the fallback shows
        private bool Apply(Sprite sprite)
        {
            UsesFallback = sprite == null;
            glyph.sprite = sprite;
            glyph.enabled = !UsesFallback;
            if (disc != null) disc.gameObject.SetActive(UsesFallback);
            letter.gameObject.SetActive(UsesFallback);
            return UsesFallback;
        }

        // Back to the roles SceneBuilder gave (a currency painted them in its own colour)
        private void Unpaint()
        {
            KitGlyph.Paint(glyph, null);
            if (disc != null) KitGlyph.Paint(disc, null);
            var themed = letter.GetComponent<ThemedGraphic>();
            if (themed != null) themed.SetFixed(null);
        }
    }
}
