using System.Text;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The skin font (a Kenney TTF Compass staged) draws the titles itself: it is only used when it has every letter
    // of the shipped tables, the fallback being a safety net, not the look (BundledFontTests)
    public sealed class SkinFontTests
    {
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
    }
}
