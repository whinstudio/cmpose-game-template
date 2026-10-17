using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kturker.UnityBase.Core.Domain;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Kturker.UnityBase.Core.Language.Tests
{
    // The shipped tables (Resources/Strings/<language>/*.json of the core, the project kind's game and this flag)
    public sealed class LanguageTableTests
    {
        private static readonly string[] Languages = { "en", "tr", "fr", "es", "it" };
        private static readonly Regex Placeholder = new Regex("\\{\\d+\\}");

        private static Dictionary<string, Dictionary<string, string>> Load(string language)
        {
            var files = new Dictionary<string, Dictionary<string, string>>();
            foreach (var asset in Resources.LoadAll<TextAsset>($"Strings/{language}"))
            {
                Assert.That(JsonText.TryParse(asset.text, out var parsed) && parsed is Dictionary<string, object>, Is.True, $"{language}/{asset.name}");
                files[asset.name] = ((Dictionary<string, object>)parsed).ToDictionary(e => e.Key, e => (string)e.Value);
            }
            return files;
        }

        [Test]
        public void Every_language_has_the_english_files_and_keys_with_the_same_placeholders()
        {
            var english = Load("en");
            var shipped = new List<string> { "core" };
            // cmpose:sample
            shipped.Add("sample");
            // /cmpose:sample
            // cmpose:showcase
            shipped.Add("showcase");
            // /cmpose:showcase
            // cmpose:runner2D
            shipped.Add("runner2d");
            // /cmpose:runner2D
            // cmpose:track3D
            shipped.Add("track3d");
            // /cmpose:track3D
            Assert.That(english.Keys, Is.SupersetOf(shipped));
            foreach (var language in Languages.Skip(1))
            {
                var other = Load(language);
                Assert.That(other.Keys, Is.EquivalentTo(english.Keys), language);
                foreach (var file in english)
                {
                    Assert.That(other[file.Key].Keys, Is.EquivalentTo(file.Value.Keys), $"{language}/{file.Key}");
                    foreach (var entry in file.Value)
                    {
                        var expected = Placeholder.Matches(entry.Value).Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                        var actual = Placeholder.Matches(other[file.Key][entry.Key]).Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                        Assert.That(actual, Is.EqualTo(expected), $"{language}/{file.Key}: {entry.Key}");
                    }
                }
            }
        }

        [Test]
        public void Every_character_of_every_table_is_in_the_font()
        {
            var font = TMP_Settings.defaultFontAsset;
            Assert.That(font, Is.Not.Null);
            var text = TableCharacters();
            // Adding characters would write glyphs into the dynamic fallback asset, so its source font is asked instead
            font.HasCharacters(text, out var notInAtlas, true, false);
            var missing = (notInAtlas ?? new uint[0]).Where(c => !InSourceFont(font, c)).ToArray();
            Assert.That(missing, Is.Empty,
                $"missing in {font.name} and its fallbacks: {string.Concat(missing.Select(c => char.ConvertFromUtf32((int)c)))}");
        }

        private static string TableCharacters() =>
            new string(Languages.SelectMany(l => Load(l).Values.SelectMany(f => f.Values))
                .SelectMany(v => v).Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());

        private static bool InSourceFont(TMP_FontAsset font, uint unicode) =>
            Chain(font).Any(f => f.atlasPopulationMode != AtlasPopulationMode.Static && f.sourceFontFile != null
                && FontEngine.LoadFontFace(f.sourceFontFile) == FontEngineError.Success
                && FontEngine.TryGetGlyphIndex(unicode, out var glyph) && glyph != 0);

        private static IEnumerable<TMP_FontAsset> Chain(TMP_FontAsset font) =>
            new[] { font }.Concat((font.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Where(f => f != null).SelectMany(Chain));
    }
}
