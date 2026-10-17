using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Colours the Graphic (Image, TMP text) on this object from its role; UiBinder binds it. A tone darkens the role's
    // colour (shade 0..1 towards black: the base under a raised button) and scales its alpha (opacity: a shadow).
    public sealed class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private ThemeRole role;
        [SerializeField, Range(0f, 1f)] private float shade;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        private Graphic graphic;
        private IThemeSource source;

        public ThemeRole Role => role;
        public float Shade => shade;
        public float Opacity => opacity;

        // Changes the role at run time (a selected row, say) and recolours from the bound theme
        public void SetRole(ThemeRole value)
        {
            role = value;
            Apply();
        }

        public void SetTone(float shadeValue, float opacityValue)
        {
            shade = Mathf.Clamp01(shadeValue);
            opacity = Mathf.Clamp01(opacityValue);
            Apply();
        }

        // The colour of a role in a palette after a tone
        public static Color Tone(Color color, float shade, float opacity)
        {
            var toned = Color.Lerp(color, Color.black, shade);
            toned.a = color.a * opacity;
            return toned;
        }

        public void Bind(IThemeSource value)
        {
            if (source != value)
            {
                if (source != null) source.Changed -= Apply;
                source = value;
                if (source != null) source.Changed += Apply;
            }
            Apply();
        }

        private void OnDestroy()
        {
            if (source != null) source.Changed -= Apply;
        }

        private void Apply()
        {
            if (!this || source == null) return;
            if (graphic == null) graphic = GetComponent<Graphic>();
            graphic.color = Tone(source.Current.Get(role), shade, opacity);
        }
    }
}
