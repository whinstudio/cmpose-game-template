using System;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Sets the font of the TMP text on this object from its role and the theme's style; UiBinder binds it. A style
    // without that font leaves the text's own.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ThemedText : MonoBehaviour
    {
        [SerializeField] private TextRole role = TextRole.Body;

        public TextRole Role => role;

        // Fires after Bind set another font (KitButton re-centres its label's capitals on it)
        public event Action FontChanged;

        // Sets the role before binding (KitFactory); Bind applies it
        public void SetRole(TextRole value) => role = value;

        public void Bind(IThemeSource source)
        {
            if (!this || source == null || source.Style == null) return;
            var font = source.Style.FontFor(role);
            var text = GetComponent<TMP_Text>();
            if (font == null || text.font == font) return;
            text.font = font;
            FontChanged?.Invoke();
        }
    }
}
