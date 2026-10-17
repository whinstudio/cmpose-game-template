using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.Core.Domain;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The style families' fonts (Core/Presentation/Fonts) have static atlases: nothing is added at run time, so each
    // atlas must hold every character of the shipped tables. Without the multiLang flag only the English tables are
    // shipped; the Turkish letters and digits are checked either way.
    public sealed class BundledFontTests
    {
        private static readonly string[] Languages = { "en", "tr", "fr", "es", "it" };
        private const string BundledFonts = "Assets/_Project/Core/Presentation/Fonts";
        private const string Required = "İıĞğŞşÇçÖöÜü0123456789";

        private static TMP_FontAsset[] Fonts()
        {
            var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { BundledFonts })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Assert.That(fonts, Is.Not.Empty, BundledFonts);
            return fonts;
        }

        private static string TableCharacters()
        {
            var values = new List<string>();
            foreach (var language in Languages)
            foreach (var asset in Resources.LoadAll<TextAsset>($"Strings/{language}"))
            {
                Assert.That(JsonText.TryParse(asset.text, out var parsed) && parsed is Dictionary<string, object>, Is.True, $"{language}/{asset.name}");
                values.AddRange(((Dictionary<string, object>)parsed).Values.Cast<string>());
            }
            Assert.That(values, Is.Not.Empty, "Resources/Strings/en");
            return new string(values.SelectMany(v => v).Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());
        }

        [Test]
        public void Every_character_of_every_table_is_in_every_bundled_font()
        {
            var text = TableCharacters() + Required;
            foreach (var font in Fonts())
            {
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), font.name);
                // The atlas itself, not its fallback: the fallback is a safety net, not the look
                font.HasCharacters(text, out var missing, false, false);
                Assert.That(missing ?? new uint[0], Is.Empty,
                    $"missing in {font.name}: {string.Concat((missing ?? new uint[0]).Select(c => char.ConvertFromUtf32((int)c)))}");
            }
        }

        [Test]
        public void Every_bundled_font_falls_back_to_a_dynamic_font()
        {
            foreach (var font in Fonts())
            {
                var fallbacks = font.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
                Assert.That(fallbacks, Is.Not.Empty, font.name);
                Assert.That(fallbacks, Has.None.Null, font.name);
                Assert.That(fallbacks.SelectMany(Chain).Any(f => f.atlasPopulationMode == AtlasPopulationMode.Dynamic), Is.True, font.name);
            }
        }

        // TMP writes these into the material the first time a text draws in the editor; unsettled, playing the game
        // would leave the font asset modified (FontBuilder.SettleFontMaterials)
        [Test]
        public void Every_bundled_font_material_has_its_scale_ratios_settled()
        {
            foreach (var font in Fonts())
            {
                var copy = new Material(font.material);
                ShaderUtilities.UpdateShaderRatios(copy);
                foreach (var ratio in new[] { "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC" })
                    Assert.That(font.material.GetFloat(ratio), Is.EqualTo(copy.GetFloat(ratio)).Within(1e-5f), $"{font.name} {ratio}");
                Object.DestroyImmediate(copy);
            }
        }

        private static IEnumerable<TMP_FontAsset> Chain(TMP_FontAsset font) =>
            new[] { font }.Concat((font.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Where(f => f != null).SelectMany(Chain));
    }
}
