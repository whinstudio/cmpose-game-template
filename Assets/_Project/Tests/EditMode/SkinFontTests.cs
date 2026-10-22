using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The skin font (a Kenney TTF Compass staged) draws the titles itself: it is only used when it has every letter
    // of the shipped tables, the fallback being a safety net, not the look (BundledFontTests). The fonts a skin names
    // by id (SkinFonts) and the fonts ThemeStyle hands each text role under a skin.
    public sealed class SkinFontTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        // A game whose skin names no skin font may delete that font's folder (MAINTAINING.md, "Skin fonts"); a folder
        // that is there must load
        private static bool Removed(string id)
        {
            var path = SkinFonts.ResourcePath(id);
            return !AssetDatabase.IsValidFolder("Assets/_Project/Core/Presentation/Fonts/Resources/" + path.Substring(0, path.LastIndexOf('/')));
        }

        private static GameSkin Skin(string json, Font font = null)
        {
            var skin = GameSkin.Parse(json, _ => null, _ => font);
            Assert.That(skin, Is.Not.Null, json);
            GameSkin.ForTests(skin);
            return skin;
        }

        [Test]
        public void Skin_font_has_every_character_of_every_table()
        {
            GameSkin.ForTests(null);
            var font = GameSkin.Current?.FontSource;
            if (font == null) Assert.Pass("no skin font");
            var missing = new StringBuilder();
            foreach (var c in BundledFontTests.TableCharacters() + BundledFontTests.Required)
                if (!font.HasCharacter(c)) missing.Append(c);
            Assert.That(missing.ToString(), Is.Empty, $"missing in {font.name}");
        }

        [Test]
        public void Every_catalogue_id_loads_a_static_font()
        {
            Assert.That(SkinFonts.Ids, Is.Not.Empty);
            Assert.That(SkinFonts.ResourcePath(SkinFonts.PixelifySans), Is.EqualTo("SkinFonts/PixelifySans/PixelifySans-Regular SDF"));
            Assert.That(SkinFonts.ResourcePath("comic-2099"), Is.Null);
            Assert.That(SkinFonts.ResourcePath(null), Is.Null);
            foreach (var id in SkinFonts.Ids)
            {
                if (Removed(id)) continue;
                var font = SkinFonts.Load(id);
                Assert.That(font, Is.Not.Null, id);
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), id);
            }
        }

        [Test]
        public void Pixelify_has_the_turkish_letters_and_digits()
        {
            if (Removed(SkinFonts.PixelifySans)) Assert.Pass("Pixelify Sans removed from this game");
            var font = SkinFonts.Load(SkinFonts.PixelifySans);
            Assert.That(font, Is.Not.Null);
            // The atlas itself, not its fallback
            font.HasCharacters(BundledFontTests.Required + BundledFontTests.TableCharacters(), out var missing, false, false);
            Assert.That(missing ?? new uint[0], Is.Empty,
                $"missing: {string.Concat((missing ?? new uint[0]).Select(c => char.ConvertFromUtf32((int)c)))}");
        }

        [Test]
        public void Skin_fonts_set_titles_and_body()
        {
            if (Removed(SkinFonts.PixelifySans)) Assert.Pass("Pixelify Sans removed from this game");
            Skin("{\"version\":2,\"fonts\":{\"display\":\"pixelify-sans\",\"body\":\"pixelify-sans\"}}");
            var style = ThemeStyle.Load();
            var pixelify = SkinFonts.Load(SkinFonts.PixelifySans);

            Assert.That(pixelify, Is.Not.Null);
            foreach (var role in new[] { TextRole.Display, TextRole.Title, TextRole.Body, TextRole.Caption })
                Assert.That(style.FontFor(role), Is.SameAs(pixelify), role.ToString());
        }

        [Test]
        public void Display_only_skin_keeps_the_body_font()
        {
            Skin("{\"version\":2,\"fonts\":{\"display\":\"pixelify-sans\"}}");
            var style = ThemeStyle.Load();

            Assert.That(style.FontFor(TextRole.Body), Is.SameAs(style.BodyFont));
            Assert.That(style.FontFor(TextRole.Caption), Is.SameAs(style.BodyFont));
        }

        // A newer Compass may name a font this template does not have: the theme's fonts, never boxes or Liberation Sans
        [Test]
        public void Unknown_font_id_keeps_the_theme_fonts()
        {
            Skin("{\"version\":2,\"fonts\":{\"display\":\"comic-2099\",\"body\":\"comic-2099\"}}");
            var style = ThemeStyle.Load();

            Assert.That(style.FontFor(TextRole.Display), Is.SameAs(style.DisplayFont));
            Assert.That(style.FontFor(TextRole.Title), Is.SameAs(style.DisplayFont));
            Assert.That(style.FontFor(TextRole.Body), Is.SameAs(style.BodyFont));
        }

        [Test]
        public void Font_id_wins_over_the_v1_font_path()
        {
            if (Removed(SkinFonts.PixelifySans)) Assert.Pass("Pixelify Sans removed from this game");
            // A stand-in for the Kenney TTF the version 1 path names
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Core/Presentation/Fonts/Inter/Inter-Regular.ttf");
            Assert.That(source, Is.Not.Null);
            var skin = Skin(SkinSprites.Json.Replace("{\"version\":1,", "{\"version\":2,\"fonts\":{\"display\":\"pixelify-sans\"},"), source);
            Assert.That(skin.FontSource, Is.SameAs(source));

            var title = ThemeStyle.Load().FontFor(TextRole.Title);
            if (title != null && title != SkinFonts.Load(SkinFonts.PixelifySans)) made.Add(title);
            Assert.That(title, Is.SameAs(SkinFonts.Load(SkinFonts.PixelifySans)));
        }
    }
}
