using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // A dark outline around a TMP text (the chunky look's button labels, a card's title on its band): the text draws with
    // a copy of its font's material that turns the SDF shader's outline on (and with an underlay, a soft shadow below).
    // One copy per font material, colour, width and underlay, shared by every text that wants the same outline; the font
    // asset's own material is never changed. A new font resets TMP's material, so when the text's ThemedText changes its
    // font (FontChanged) the outline moves onto the new font's copy. Clear gives the text its font's own material back.
    // The bundled fonts draw with TMP's mobile SDF shader; its OUTLINE_ON and UNDERLAY_ON variants reach a player build
    // through TMP's own outline and drop shadow materials (TextMesh Pro/Resources).
    public static class KitTextOutline
    {
        // The underlay: down and a little to the right, soft, the outline's colour faded (the shader's units: a share
        // of the SDF's spread)
        public const float UnderlayOffsetX = 0.2f;
        public const float UnderlayOffsetY = -0.6f;
        public const float UnderlaySoftness = 0.3f;
        public const float UnderlayOpacity = 0.6f;

        private static readonly Dictionary<(int, Color, float, bool), Material> Materials =
            new Dictionary<(int, Color, float, bool), Material>();
        private static readonly HashSet<Material> Owned = new HashSet<Material>();
        // What each outlined text wants, kept for its ThemedText's FontChanged (weak: a destroyed text drops out)
        private static readonly ConditionalWeakTable<TMP_Text, Wanted> Outlined = new ConditionalWeakTable<TMP_Text, Wanted>();

        private sealed class Wanted
        {
            public Color Colour;
            public float Width;
            public bool Underlay;
            public ThemedText Followed;
            public Action Handler;
        }

        // Draws text with an outline of colour outline and width (the shader's _OutlineWidth, 0..1) and, with underlay, a
        // soft shadow below in the same colour; kept through the text's font changes
        public static void Apply(TMP_Text text, Color outline, float width, bool underlay)
        {
            if (text == null) return;
            var wanted = Outlined.GetValue(text, _ => new Wanted());
            wanted.Colour = outline;
            wanted.Width = width;
            wanted.Underlay = underlay;
            Follow(text, wanted);
            Put(text, wanted);
        }

        // The text's font's own material again if it draws with an outline's (a material set by other code stays); no
        // longer kept through font changes
        public static void Clear(TMP_Text text)
        {
            if (text == null) return;
            if (Outlined.TryGetValue(text, out var wanted))
            {
                if (wanted.Followed != null) wanted.Followed.FontChanged -= wanted.Handler;
                Outlined.Remove(text);
            }
            if (!Has(text)) return;
            var font = text.font;
            if (font != null && font.material != null) text.fontSharedMaterial = font.material;
        }

        // Whether the text draws with an outline's material
        public static bool Has(TMP_Text text)
        {
            if (text == null) return false;
            var material = text.fontSharedMaterial;
            return material != null && Owned.Contains(material);
        }

        // Tests only: destroys the cached materials and forgets the outlined texts, so a fixture leaves none behind (its
        // texts are gone by then)
        internal static void DestroyMaterialsForTests()
        {
            // The outlined texts too: their FontChanged handlers would pile up across fixtures
            foreach (var pair in Outlined)
                if (!ReferenceEquals(pair.Value.Followed, null)) pair.Value.Followed.FontChanged -= pair.Value.Handler;
            Outlined.Clear();
            foreach (var material in Owned)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            Owned.Clear();
            Materials.Clear();
        }

        private static void Follow(TMP_Text text, Wanted wanted)
        {
            var themed = text.GetComponent<ThemedText>();
            if (themed == wanted.Followed) return;
            if (wanted.Followed != null) wanted.Followed.FontChanged -= wanted.Handler;
            wanted.Followed = themed;
            if (themed == null) return;
            if (wanted.Handler == null) wanted.Handler = () => Put(text, wanted);
            themed.FontChanged += wanted.Handler;
        }

        private static void Put(TMP_Text text, Wanted wanted)
        {
            if (!text) return;
            var font = text.font;
            if (font == null || font.material == null) return;
            var material = MaterialFor(font.material, wanted.Colour, wanted.Width, wanted.Underlay);
            if (text.fontSharedMaterial != material) text.fontSharedMaterial = material;
        }

        private static Material MaterialFor(Material source, Color colour, float width, bool underlay)
        {
            var key = (source.GetInstanceID(), colour, width, underlay);
            if (Materials.TryGetValue(key, out var made) && made != null) return made;
            made = new Material(source)
            {
                name = $"{source.name} Outline",
                // Made at run time: never saved with a scene or prefab, never unloaded under a text that uses it
                hideFlags = HideFlags.DontSave
            };
            made.EnableKeyword(ShaderUtilities.Keyword_Outline);
            made.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            made.SetColor(ShaderUtilities.ID_OutlineColor, colour);
            if (underlay)
            {
                made.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                var shadow = colour;
                shadow.a *= UnderlayOpacity;
                made.SetColor(ShaderUtilities.ID_UnderlayColor, shadow);
                made.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, UnderlayOffsetX);
                made.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, UnderlayOffsetY);
                made.SetFloat(ShaderUtilities.ID_UnderlaySoftness, UnderlaySoftness);
            }
            else
            {
                made.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }
            ShaderUtilities.UpdateShaderRatios(made);
            Materials[key] = made;
            Owned.Add(made);
            return made;
        }
    }
}
