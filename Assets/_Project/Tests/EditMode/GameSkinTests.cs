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
        public void Button_variant_maps_to_skin_entry()
        {
            var skin = Parse(SkinSprites.Json);

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
