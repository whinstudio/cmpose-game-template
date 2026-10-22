using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
// cmpose:uiTheme
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Core.Theme;
// /cmpose:uiTheme
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class SkinPaletteTests
    {
        private static readonly Color Amber = new Color32(0xC9, 0x82, 0x2C, 0xFF);
        private static readonly Color Cream = new Color32(0xF4, 0xE6, 0xC8, 0xFF);

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private Palette MakePalette()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            palette.name = "Test";
            palette.Set(ThemeRole.Primary, Color.blue);
            palette.Set(ThemeRole.Text, Color.black);
            palette.Set(ThemeRole.Danger, Color.red);
            palette.Set(ThemeRole.Picture, Color.white);
            made.Add(palette);
            return palette;
        }

        private GameSkin V2()
        {
            var sprites = SkinSprites.MakeV2(made);
            return GameSkin.Parse(SkinSprites.V2, path => sprites.TryGetValue(path, out var sprite) ? sprite : null, _ => null);
        }

        private Palette Dressed(Palette palette, GameSkin skin)
        {
            var dressed = Palettes.Dress(palette, skin);
            if (dressed != palette) made.Add(dressed);
            return dressed;
        }

        // Resources.Load under the test skin; the copy it makes is cleaned up
        private Palette Load(string path)
        {
            var palette = Palettes.Load(path);
            if (palette != Resources.Load<Palette>(path)) made.Add(palette);
            return palette;
        }

        [Test]
        public void Without_a_skin_the_asset_is_handed_out()
        {
            var asset = MakePalette();
            Assert.That(Palettes.Dress(asset, null), Is.SameAs(asset));
            Assert.That(Palettes.Dress(asset, GameSkin.Parse(SkinSprites.Json, _ => null, _ => null)), Is.SameAs(asset));
            Assert.That(Palettes.Dress(asset, GameSkin.Parse("{\"version\":2}", _ => null, _ => null)), Is.SameAs(asset));
        }

        [Test]
        public void Named_roles_take_the_skin_colour()
        {
            var asset = MakePalette();
            var dressed = Dressed(asset, V2());

            Assert.That(dressed, Is.Not.SameAs(asset));
            Assert.That(dressed.name, Is.EqualTo("Test (skin)"));
            Assert.That(dressed.Get(ThemeRole.Primary), Is.EqualTo(Amber));
            Assert.That(dressed.Get(ThemeRole.Text), Is.EqualTo(Cream));
            Assert.That(dressed.Get(ThemeRole.Danger), Is.EqualTo(Color.red));
            Assert.That(dressed.Get(ThemeRole.Picture), Is.EqualTo(Color.white));
        }

        [Test]
        public void The_palette_asset_is_never_changed()
        {
            var asset = MakePalette();
            Dressed(asset, V2());
            Assert.That(asset.Get(ThemeRole.Primary), Is.EqualTo(Color.blue));
            Assert.That(asset.Get(ThemeRole.Text), Is.EqualTo(Color.black));
        }

        [Test]
        public void Load_hands_out_the_dressed_light_palette()
        {
            var asset = Resources.Load<Palette>(Palettes.Light);
            var before = asset.Get(ThemeRole.Primary);
            GameSkin.ForTests(V2());

            Assert.That(Load(Palettes.Light).Get(ThemeRole.Primary), Is.EqualTo(Amber));
            Assert.That(asset.Get(ThemeRole.Primary), Is.EqualTo(before), "the asset");
        }

        // cmpose:uiTheme
        private sealed class FakeSystemDarkMode : ISystemDarkMode
        {
            public bool IsDark { get; set; }
        }

        [Test]
        public void Dark_palette_is_dressed_too()
        {
            GameSkin.ForTests(V2());
            Assert.That(Load(ThemeInstaller.DarkPalette).Get(ThemeRole.Primary), Is.EqualTo(Amber));

            var theme = new SwitchableThemeSource(new FakePreferences(), new FakeSystemDarkMode(), new PauseService(), new FakeLog(),
                Load(Palettes.Light), Load(ThemeInstaller.DarkPalette), ThemeStyle.Load());
            try
            {
                theme.Start();
                theme.SetMode(ThemeMode.Light);
                Assert.That(theme.Current.Get(ThemeRole.Primary), Is.EqualTo(Amber), "light");
                theme.SetMode(ThemeMode.Dark);
                Assert.That(theme.Current.Get(ThemeRole.Primary), Is.EqualTo(Amber), "dark");
            }
            finally
            {
                theme.Dispose();
            }
        }
        // /cmpose:uiTheme

        [Test]
        public void Kit_parts_repaint_with_the_skin_palette()
        {
            // The palette alone: a skinned button draws its sprite in its own colours
            GameSkin.ForTests(GameSkin.Parse("{\"version\":2,\"palette\":{\"Primary\":\"#C9822C\"}}", _ => null, _ => null));
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(new FixedThemeSource(Load(Palettes.Light), ThemeStyle.Load()));

            Assert.That(button.Body.color, Is.EqualTo(Amber));
        }
    }
}
