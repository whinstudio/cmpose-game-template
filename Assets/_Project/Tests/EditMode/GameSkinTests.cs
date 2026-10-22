using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class GameSkinTests
    {
        private readonly List<Object> made = new List<Object>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        [SetUp]
        public void MakeSprites()
        {
            foreach (var name in new[] { "blue", "blue_pressed", "grey", "grey_pressed", "red", "red_pressed", "yellow", "yellow_pressed", "panel", "sheet", "dialog" })
                sprites[$"Art/pixel-ui-pack/{name}"] = SkinSprites.Make(made, name);
        }

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            sprites.Clear();
        }

        private GameSkin Parse(string json) => GameSkin.Parse(json, Sprite, _ => null);

        private Sprite Sprite(string path) => sprites.TryGetValue(path, out var sprite) ? sprite : null;

        // The template ships without Resources/GameSkin.json; a game Compass dressed has one, so Current follows the file
        [Test]
        public void Absent_skin_is_null()
        {
            Assert.That(GameSkin.Read(null), Is.Null);
            GameSkin.ForTests(null);
            var file = Resources.Load<TextAsset>(GameSkin.ResourcePath);
            if (file == null) Assert.That(GameSkin.Current, Is.Null);
        }

        [Test]
        public void Invalid_json_is_ignored()
        {
            Assert.That(Parse("{\"version\":1,\"ui\":"), Is.Null, "truncated");
            Assert.That(Parse("<html>"), Is.Null, "not json");
            Assert.That(Parse("[1,2]"), Is.Null, "not an object");
            Assert.That(Parse(""), Is.Null, "empty");
            Assert.That(Parse(null), Is.Null, "null");
        }

        [Test]
        public void Missing_sprite_drops_only_the_ui_section()
        {
            var skin = Parse(SkinSprites.Json.Replace("Art/pixel-ui-pack/dialog", "Art/pixel-ui-pack/missing"));

            Assert.That(skin, Is.Not.Null);
            Assert.That(skin.BrandBackground, Is.EqualTo((Color?)new Color32(0x12, 0x34, 0x56, 0xFF)), "brand stays");
            foreach (var variant in new[] { KitButtonVariant.Primary, KitButtonVariant.Secondary, KitButtonVariant.Icon, KitButtonVariant.Play })
                Assert.That(skin.Button(variant), Is.Null, $"{variant}");
            Assert.That(skin.Card, Is.Null);
            Assert.That(skin.Sheet, Is.Null);
            Assert.That(skin.Dialog, Is.Null);
        }

        [Test]
        public void Button_variant_maps_to_skin_entry() => AssertV1Parts(Parse(SkinSprites.Json), sprites);

        private static void AssertV1Parts(GameSkin skin, Dictionary<string, Sprite> sprites)
        {
            Assert.That(skin.Button(KitButtonVariant.Primary).Normal, Is.SameAs(sprites["Art/pixel-ui-pack/blue"]));
            Assert.That(skin.Button(KitButtonVariant.Primary).Pressed, Is.SameAs(sprites["Art/pixel-ui-pack/blue_pressed"]));
            Assert.That(skin.Button(KitButtonVariant.Primary).Text, Is.EqualTo((Color)new Color32(0xFF, 0xFF, 0xFF, 0xFF)));
            Assert.That(skin.Button(KitButtonVariant.Secondary).Normal, Is.SameAs(sprites["Art/pixel-ui-pack/grey"]));
            Assert.That(skin.Button(KitButtonVariant.Secondary).Text, Is.EqualTo((Color)new Color32(0x22, 0x22, 0x22, 0xFF)));
            Assert.That(skin.Button(KitButtonVariant.Play).Normal, Is.SameAs(sprites["Art/pixel-ui-pack/yellow"]));
            Assert.That(skin.Button(KitButtonVariant.Icon).Pressed, Is.SameAs(sprites["Art/pixel-ui-pack/red_pressed"]));
            Assert.That(skin.Card, Is.SameAs(sprites["Art/pixel-ui-pack/panel"]));
            Assert.That(skin.Sheet, Is.SameAs(sprites["Art/pixel-ui-pack/sheet"]));
            Assert.That(skin.Dialog, Is.SameAs(sprites["Art/pixel-ui-pack/dialog"]));
            Assert.That(skin.SliceScale, Is.EqualTo(4f));
            Assert.That(skin.Pixel, Is.True);
        }

        // Compass writes no icon entry when the pack has no round button: the rest of the ui stays
        [Test]
        public void Skin_without_icon_entry_keeps_the_ui()
        {
            var skin = Parse(SkinSprites.WithoutIcon);

            Assert.That(skin.Button(KitButtonVariant.Icon), Is.Null);
            Assert.That(skin.Button(KitButtonVariant.Primary), Is.Not.Null);
            Assert.That(skin.Button(KitButtonVariant.Secondary), Is.Not.Null);
            Assert.That(skin.Button(KitButtonVariant.Play), Is.Not.Null);
            Assert.That(skin.Card, Is.SameAs(sprites["Art/pixel-ui-pack/panel"]));
        }

        // An entry that is there but half written is a broken file
        [Test]
        public void Broken_icon_entry_drops_the_ui()
        {
            var cases = new[]
            {
                ("missing sprite", SkinSprites.Json.Replace("Art/pixel-ui-pack/red_pressed", "Art/pixel-ui-pack/missing")),
                ("bad colour", SkinSprites.Json.Replace("#FFEEDD", "#FFEE")),
                ("not an object", SkinSprites.WithoutIcon.Replace("\"buttons\":{", "\"buttons\":{\"icon\":null,"))
            };
            foreach (var (what, json) in cases)
            {
                var skin = Parse(json);
                Assert.That(skin, Is.Not.Null, what);
                Assert.That(skin.Button(KitButtonVariant.Primary), Is.Null, what);
                Assert.That(skin.Card, Is.Null, what);
                Assert.That(skin.BrandBackground, Is.Not.Null, $"{what}: brand stays");
            }
        }

        [Test]
        public void Brand_background_is_parsed()
        {
            Assert.That(Parse("{\"version\":1,\"brand\":{\"background\":\"#123456\"}}").BrandBackground,
                Is.EqualTo((Color?)new Color32(0x12, 0x34, 0x56, 0xFF)));
            var brandOnly = Parse("{\"version\":1,\"brand\":{\"background\":\"not a colour\"}}");
            Assert.That(brandOnly, Is.Not.Null);
            Assert.That(brandOnly.BrandBackground, Is.Null, "a bad colour is left out");
            Assert.That(brandOnly.Button(KitButtonVariant.Primary), Is.Null, "no ui section");
        }

        [Test]
        public void A_font_that_does_not_load_drops_only_the_font()
        {
            var skin = Parse(SkinSprites.Json);
            Assert.That(skin.FontSource, Is.Null);
            Assert.That(skin.DisplayFont, Is.Null);
            Assert.That(skin.Button(KitButtonVariant.Primary), Is.Not.Null, "the ui stays");
        }

        [Test]
        public void A_bad_slice_scale_drops_the_ui_section()
        {
            var skin = Parse(SkinSprites.Json.Replace("\"sliceScale\":4", "\"sliceScale\":0"));
            Assert.That(skin.Button(KitButtonVariant.Primary), Is.Null);
            Assert.That(skin.BrandBackground, Is.Not.Null);
        }

        private static GameSkin ParseV2(string json, Dictionary<string, Sprite> map) =>
            GameSkin.Parse(json, path => map.TryGetValue(path, out var sprite) ? sprite : null, _ => null);

        [Test]
        public void Missing_version_reads_as_one()
        {
            var skin = Parse("{\"brand\":{\"background\":\"#123456\"}}");
            Assert.That(skin.Version, Is.EqualTo(1));
            Assert.That(skin.BrandBackground, Is.EqualTo((Color?)new Color32(0x12, 0x34, 0x56, 0xFF)));
            Assert.That(Parse(SkinSprites.Json).Version, Is.EqualTo(1));
        }

        // A file from a newer Compass is read as no skin rather than half understood
        [Test]
        public void Unknown_version_is_no_skin()
        {
            Assert.That(Parse("{\"version\":3,\"brand\":{\"background\":\"#123456\"}}"), Is.Null, "3");
            Assert.That(Parse("{\"version\":0}"), Is.Null, "0");
            Assert.That(Parse("{\"version\":1.5}"), Is.Null, "1.5");
            Assert.That(Parse("{\"version\":\"2\"}"), Is.Null, "a string");
        }

        [Test]
        public void Version_two_reads_every_section()
        {
            var map = SkinSprites.MakeV2(made);
            var skin = ParseV2(SkinSprites.V2, map);

            Assert.That(skin.Version, Is.EqualTo(2));
            Assert.That(skin.Palette.Count, Is.EqualTo(11));
            Assert.That(skin.Palette[ThemeRole.Primary], Is.EqualTo((Color)new Color32(0xC9, 0x82, 0x2C, 0xFF)));
            Assert.That(skin.Palette[ThemeRole.OnAccent], Is.EqualTo((Color)new Color32(0x3B, 0x24, 0x10, 0xFF)));
            Assert.That(skin.MenuBackground, Is.SameAs(map["Art/sample/backgrounds/menu"]));
            Assert.That(skin.GameBackground, Is.SameAs(map["Art/sample/backgrounds/game"]));
            Assert.That(skin.Scrim, Is.EqualTo((Color?)new Color32(0x14, 0x28, 0x50, 0x47)));
            Assert.That(skin.DisplayFontId, Is.EqualTo("pixelify-sans"));
            Assert.That(skin.BodyFontId, Is.EqualTo("pixelify-sans"));
            Assert.That(skin.Pill.Sprite, Is.SameAs(map["Art/sample/pill"]));
            Assert.That(skin.Pill.Text, Is.EqualTo((Color?)new Color32(0x2B, 0x3A, 0x1F, 0xFF)));
            Assert.That(skin.Slider.Track, Is.SameAs(map["Art/sample/slider_track"]));
            Assert.That(skin.Slider.Fill, Is.SameAs(map["Art/sample/slider_fill"]));
            Assert.That(skin.Slider.Knob, Is.SameAs(map["Art/sample/slider_knob"]));
            Assert.That(skin.Switch.Track, Is.SameAs(map["Art/sample/switch_track"]));
            Assert.That(skin.Switch.Knob, Is.SameAs(map["Art/sample/switch_knob"]));
            Assert.That(skin.Progress.Track, Is.SameAs(map["Art/sample/progress_track"]));
            Assert.That(skin.Progress.Fill, Is.SameAs(map["Art/sample/progress_fill"]));
            Assert.That(skin.Star.Full, Is.SameAs(map["Art/sample/star_full"]));
            Assert.That(skin.Star.Empty, Is.SameAs(map["Art/sample/star_empty"]));
            Assert.That(skin.Icon("coin"), Is.SameAs(map["Art/sample/icon_coin"]));
            Assert.That(skin.Icon("key"), Is.SameAs(map["Art/sample/icon_key"]));
            Assert.That(skin.Icon("nope"), Is.Null);
            Assert.That(skin.Digits.Length, Is.EqualTo(10));
            for (var i = 0; i < 10; i++)
                Assert.That(skin.Digits[i], Is.SameAs(map[$"Art/sample/hud{i}"]), $"digit {i}");
            AssertV1Parts(skin, map);
        }

        [Test]
        public void Palette_ignores_unknown_roles_and_bad_hex()
        {
            var skin = Parse("{\"version\":2,\"palette\":{\"Primary\":\"#zz\",\"Glow\":\"#FFFFFF\",\"text\":\"#FFFFFF\",\"2\":\"#FFFFFF\",\"Text\":\"#101010\"}}");

            Assert.That(skin.Palette.Count, Is.EqualTo(1), "a wrong case or a role number is not a role");
            Assert.That(skin.Palette[ThemeRole.Text], Is.EqualTo((Color)new Color32(0x10, 0x10, 0x10, 0xFF)));
        }

        [Test]
        public void Palette_accepts_alpha_hex()
        {
            var skin = Parse("{\"version\":2,\"palette\":{\"Scrim\":\"#00000080\"}}");
            Assert.That(skin.Palette[ThemeRole.Scrim].a, Is.EqualTo(128f / 255f).Within(1e-6f));
        }

        // Each case removes one sprite the file names: only the part that needs it goes
        [Test]
        public void A_broken_new_section_drops_only_itself()
        {
            var cases = new[]
            {
                ("Art/sample/backgrounds/menu", "menu"),
                ("Art/sample/pill", "pill"),
                ("Art/sample/slider_knob", "slider"),
                ("Art/sample/switch_track", "switch"),
                ("Art/sample/progress_fill", "progress"),
                ("Art/sample/star_empty", "star"),
                ("Art/sample/icon_heart", "heart"),
                ("Art/sample/hud7", "digits")
            };
            foreach (var (path, part) in cases)
            {
                var map = SkinSprites.MakeV2(made);
                map.Remove(path);
                var skin = ParseV2(SkinSprites.V2, map);

                foreach (var pair in Parts(skin))
                    Assert.That(pair.Value, Is.EqualTo(pair.Key != part), $"{path} missing: {pair.Key}");
            }
        }

        private static Dictionary<string, bool> Parts(GameSkin skin) => new Dictionary<string, bool>
        {
            ["menu"] = skin.MenuBackground != null,
            ["game"] = skin.GameBackground != null,
            ["scrim"] = skin.Scrim != null,
            ["palette"] = skin.Palette.Count == 11,
            ["fonts"] = skin.DisplayFontId != null && skin.BodyFontId != null,
            ["pill"] = skin.Pill != null,
            ["slider"] = skin.Slider != null,
            ["switch"] = skin.Switch != null,
            ["progress"] = skin.Progress != null,
            ["star"] = skin.Star != null,
            ["coin"] = skin.Icon("coin") != null,
            ["heart"] = skin.Icon("heart") != null,
            ["gem"] = skin.Icon("gem") != null,
            ["key"] = skin.Icon("key") != null,
            ["digits"] = skin.Digits != null,
            ["buttons"] = skin.Button(KitButtonVariant.Primary) != null && skin.Button(KitButtonVariant.Icon) != null,
            ["panels"] = skin.Card != null && skin.Sheet != null && skin.Dialog != null
        };

        [Test]
        public void Star_needs_both_sprites()
        {
            var skin = ParseV2(SkinSprites.V2.Replace(",\"empty\":\"Art/sample/star_empty\"", ""), SkinSprites.MakeV2(made));
            Assert.That(skin.Star, Is.Null);
            Assert.That(skin.Slider, Is.Not.Null);
        }

        // A key left out is fine (the kit draws that piece itself); a bad label colour is a broken pill
        [Test]
        public void Omitted_sprites_are_kept_out_and_a_bad_pill_colour_drops_the_pill()
        {
            var map = SkinSprites.MakeV2(made);
            var withoutKnob = ParseV2(SkinSprites.V2.Replace(",\"knob\":\"Art/sample/slider_knob\"", ""), map);
            Assert.That(withoutKnob.Slider.Knob, Is.Null);
            Assert.That(withoutKnob.Slider.Track, Is.SameAs(map["Art/sample/slider_track"]));

            var badText = ParseV2(SkinSprites.V2.Replace("#2B3A1F", "#2B3A"), map);
            Assert.That(badText.Pill, Is.Null);
            Assert.That(badText.Slider, Is.Not.Null);
        }

        [Test]
        public void Broken_v1_ui_keeps_the_new_ui_parts()
        {
            var map = SkinSprites.MakeV2(made);
            map.Remove("Art/pixel-ui-pack/dialog");
            var skin = ParseV2(SkinSprites.V2, map);

            Assert.That(skin.Button(KitButtonVariant.Primary), Is.Null);
            Assert.That(skin.Card, Is.Null);
            Assert.That(skin.Slider, Is.Not.Null);
            Assert.That(skin.Pill, Is.Not.Null);
            Assert.That(skin.Digits, Is.Not.Null);
            Assert.That(skin.SliceScale, Is.EqualTo(4f));
            Assert.That(skin.Pixel, Is.True);
        }

        [Test]
        public void Bad_slice_scale_drops_every_ui_sprite()
        {
            var skin = ParseV2(SkinSprites.V2.Replace("\"sliceScale\":4", "\"sliceScale\":0"), SkinSprites.MakeV2(made));

            Assert.That(skin.Button(KitButtonVariant.Primary), Is.Null);
            Assert.That(skin.Card, Is.Null);
            Assert.That(skin.Pill?.Sprite, Is.Null);
            Assert.That(skin.Slider, Is.Null);
            Assert.That(skin.Switch, Is.Null);
            Assert.That(skin.Progress, Is.Null);
            Assert.That(skin.Star, Is.Null);
            Assert.That(skin.Icon("coin"), Is.Null);
            Assert.That(skin.Digits, Is.Null);
            Assert.That(skin.SliceScale, Is.EqualTo(1f));
            Assert.That(skin.Palette.Count, Is.EqualTo(11), "palette stays");
            Assert.That(skin.MenuBackground, Is.Not.Null, "backgrounds are not ui parts");
        }

        [Test]
        public void V1_file_has_no_v2_parts()
        {
            var skin = Parse(SkinSprites.Json);

            Assert.That(skin.Palette, Is.Empty);
            Assert.That(skin.MenuBackground, Is.Null);
            Assert.That(skin.GameBackground, Is.Null);
            Assert.That(skin.Scrim, Is.Null);
            Assert.That(skin.DisplayFontId, Is.Null);
            Assert.That(skin.BodyFontId, Is.Null);
            Assert.That(skin.Pill, Is.Null);
            Assert.That(skin.Slider, Is.Null);
            Assert.That(skin.Switch, Is.Null);
            Assert.That(skin.Progress, Is.Null);
            Assert.That(skin.Star, Is.Null);
            Assert.That(skin.Icon("coin"), Is.Null);
            Assert.That(skin.Digits, Is.Null);
            AssertV1Parts(skin, sprites);
        }
    }

    // Sprites and the skin file the skin tests share
    internal static class SkinSprites
    {
        public const string Json = @"{""version"":1,
 ""brand"":{""background"":""#123456""},
 ""ui"":{""pack"":""pixel-ui-pack"",""pixel"":true,""sliceScale"":4,""font"":""Art/kenney-fonts/Kenney Pixel"",
   ""buttons"":{
     ""primary"":{""normal"":""Art/pixel-ui-pack/blue"",""pressed"":""Art/pixel-ui-pack/blue_pressed"",""text"":""#FFFFFF""},
     ""secondary"":{""normal"":""Art/pixel-ui-pack/grey"",""pressed"":""Art/pixel-ui-pack/grey_pressed"",""text"":""#222222""},
     ""play"":{""normal"":""Art/pixel-ui-pack/yellow"",""pressed"":""Art/pixel-ui-pack/yellow_pressed"",""text"":""#331100""},
     ""icon"":{""normal"":""Art/pixel-ui-pack/red"",""pressed"":""Art/pixel-ui-pack/red_pressed"",""text"":""#FFEEDD""}},
   ""card"":""Art/pixel-ui-pack/panel"",""sheet"":""Art/pixel-ui-pack/sheet"",""dialog"":""Art/pixel-ui-pack/dialog""}}";

        // Json as a soft (not pixel) pack
        public static string Soft => Json.Replace(@"""pixel"":true", @"""pixel"":false");

        // Json without its icon button entry
        public static string WithoutIcon => Json.Replace(@",
     ""icon"":{""normal"":""Art/pixel-ui-pack/red"",""pressed"":""Art/pixel-ui-pack/red_pressed"",""text"":""#FFEEDD""}}", "}");

        // Json as version 2 with every new section, in the shared contract's colours
        public static string V2 => Json
            .Replace(@"{""version"":1,", @"{""version"":2,
 ""palette"":{""Background"":""#2A2233"",""Surface"":""#3B2A1E"",""SurfaceVariant"":""#4A3626"",""Primary"":""#C9822C"",
   ""OnPrimary"":""#FFF6DC"",""Accent"":""#E8B33A"",""OnAccent"":""#3B2410"",""Text"":""#F4E6C8"",""TextMuted"":""#BFAF94"",
   ""Track"":""#5A4632"",""Control"":""#C9822C""},
 ""backgrounds"":{""menu"":""Art/sample/backgrounds/menu"",""game"":""Art/sample/backgrounds/game"",""scrim"":""#14285047""},
 ""fonts"":{""display"":""pixelify-sans"",""body"":""pixelify-sans""},")
            .Replace(@"""dialog"":""Art/pixel-ui-pack/dialog""}}", @"""dialog"":""Art/pixel-ui-pack/dialog"",
   ""pill"":{""sprite"":""Art/sample/pill"",""text"":""#2B3A1F""},
   ""slider"":{""track"":""Art/sample/slider_track"",""fill"":""Art/sample/slider_fill"",""knob"":""Art/sample/slider_knob""},
   ""switch"":{""track"":""Art/sample/switch_track"",""knob"":""Art/sample/switch_knob""},
   ""progress"":{""track"":""Art/sample/progress_track"",""fill"":""Art/sample/progress_fill""},
   ""star"":{""full"":""Art/sample/star_full"",""empty"":""Art/sample/star_empty""},
   ""icons"":{""coin"":""Art/sample/icon_coin"",""heart"":""Art/sample/icon_heart"",""gem"":""Art/sample/icon_gem"",""key"":""Art/sample/icon_key""},
   ""digits"":""Art/sample/hud""}}");

        // A sprite for every path V2 names
        public static Dictionary<string, Sprite> MakeV2(List<Object> made)
        {
            var paths = new List<string>
            {
                "Art/sample/backgrounds/menu", "Art/sample/backgrounds/game", "Art/sample/pill",
                "Art/sample/slider_track", "Art/sample/slider_fill", "Art/sample/slider_knob", "Art/sample/switch_track",
                "Art/sample/switch_knob", "Art/sample/progress_track", "Art/sample/progress_fill", "Art/sample/star_full",
                "Art/sample/star_empty", "Art/sample/icon_coin", "Art/sample/icon_heart", "Art/sample/icon_gem", "Art/sample/icon_key"
            };
            for (var i = 0; i < 10; i++) paths.Add($"Art/sample/hud{i}");
            foreach (var name in new[] { "blue", "blue_pressed", "grey", "grey_pressed", "red", "red_pressed", "yellow", "yellow_pressed", "panel", "sheet", "dialog" })
                paths.Add($"Art/pixel-ui-pack/{name}");
            var sprites = new Dictionary<string, Sprite>();
            foreach (var path in paths) sprites[path] = Make(made, path.Substring(path.LastIndexOf('/') + 1));
            return sprites;
        }

        public static Sprite Make(List<Object> made, string name)
        {
            var texture = new Texture2D(48, 48) { name = name };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 48f, 48f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(12f, 12f, 12f, 12f));
            sprite.name = name;
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }
    }
}
