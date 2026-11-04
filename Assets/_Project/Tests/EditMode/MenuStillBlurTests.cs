using Kturker.UnityBase.Core.Presentation.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The game's still behind the menu (Resources/Branding/menu-background.png, written by Compass) is scaled down and
    // blurred when it is imported, so the world's pieces do not compete with the logo and the buttons
    public sealed class MenuStillBlurTests
    {
        private static Color32[] Picture(int width, int height, System.Func<int, int, byte> grey)
        {
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var v = grey(x, y);
                    pixels[y * width + x] = new Color32(v, v, v, 255);
                }
            return pixels;
        }

        [Test]
        public void Blur_flattens_fine_detail()
        {
            var checker = Picture(1080, 1920, (x, y) => (byte)(((x / 8) + (y / 8)) % 2 == 0 ? 0 : 255));

            var blurred = MenuStillBlur.Blur(checker, 1080, 1920, out var width, out var height);

            Assert.That(width, Is.EqualTo(270));
            Assert.That(height, Is.EqualTo(480));
            Assert.That(blurred.Length, Is.EqualTo(width * height));
            var worst = 0f;
            foreach (var p in blurred)
                foreach (var c in new[] { p.r, p.g, p.b })
                    worst = Mathf.Max(worst, Mathf.Abs(c - 127.5f));
            Assert.That(worst, Is.LessThanOrEqualTo(10f), "every pixel near mid grey");
        }

        [Test]
        public void Blur_softens_an_edge()
        {
            var edge = Picture(1080, 1920, (x, y) => (byte)(x < 540 ? 0 : 255));

            var blurred = MenuStillBlur.Blur(edge, 1080, 1920, out var width, out var height);

            var soft = 0;
            var row = height / 2;
            for (var x = 0; x < width; x++)
            {
                var v = blurred[row * width + x].r;
                if (v > 10 && v < 245) soft++;
            }
            Assert.That(soft, Is.GreaterThanOrEqualTo(20), "columns between black and white on the middle row");
        }

        [TestCase(7, 5)]
        [TestCase(1, 1)]
        public void Blur_keeps_a_small_picture_size(int width, int height)
        {
            var small = Picture(width, height, (x, y) => (byte)((x + y) % 2 == 0 ? 40 : 200));

            var blurred = MenuStillBlur.Blur(small, width, height, out var outWidth, out var outHeight);

            Assert.That(outWidth, Is.EqualTo(width));
            Assert.That(outHeight, Is.EqualTo(height));
            Assert.That(blurred.Length, Is.EqualTo(width * height));
        }

        [Test]
        public void Landscape_scales_by_its_short_side()
        {
            var landscape = Picture(1920, 1080, (x, y) => 128);

            var blurred = MenuStillBlur.Blur(landscape, 1920, 1080, out var width, out var height);

            Assert.That(width, Is.EqualTo(480));
            Assert.That(height, Is.EqualTo(270));
            Assert.That(blurred.Length, Is.EqualTo(480 * 270));
        }

        [Test]
        public void Blur_keeps_alpha()
        {
            var clear = new Color32[64 * 48];
            for (var i = 0; i < clear.Length; i++) clear[i] = new Color32(255, 0, 0, 0);

            var blurred = MenuStillBlur.Blur(clear, 64, 48, out _, out _);

            foreach (var p in blurred) Assert.That(p.a, Is.EqualTo(0));
        }

        // Only Compass's still is blurred: the title logo beside it and a skin's designed background stay sharp
        [Test]
        public void Only_the_menu_still_is_blurred()
        {
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Branding/menu-background.png"), Is.True, "the game's still");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/Editor/Maintainer/ShotFixtures/Branding/menu-background.png"), Is.True, "the shot fixture");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Branding/title-logo.png"), Is.False, "the logo");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Art/sample/backgrounds/menu.png"), Is.False, "a skin background");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Art/menu-background.png"), Is.False, "outside Branding");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Branding/menu-background.jpg"), Is.False, "not a png");
            Assert.That(MenuStillImporter.IsMenuStill("Assets/_Project/Resources/Branding/menu-scene.png"), Is.False, "the designed menu background");
        }

        // The Compass kit's importer may point-filter Branding pictures in pixel games (order 0): the still's settings win
        [Test]
        public void Menu_still_importer_runs_after_the_kit()
        {
            Assert.That(new MenuStillImporter().GetPostprocessOrder(), Is.GreaterThan(0));
        }
    }
}
