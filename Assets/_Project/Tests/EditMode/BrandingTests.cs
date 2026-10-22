using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.Boot;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The game's own title logo, menu background and boot logo (Resources/Branding, written by Compass) take the place
    // of the themed title text and decor; without them the screens look as they always did
    public sealed class BrandingTests
    {
        private readonly List<Object> made = new List<Object>();
        private Scene scene;

        [TearDown]
        public void Clean()
        {
            GameBranding.ForTests(null, null);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private Sprite MakeSprite(string name, int width, int height)
        {
            var texture = new Texture2D(width, height) { name = name };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        // Opens a scene and binds its themed graphics the way SceneUiBinding does at run time
        private Transform Open(string path)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole))) palette.Set(role, Color.gray);
            palette.Set(ThemeRole.Picture, Color.white);
            palette.Set(ThemeRole.Scrim, new Color(0f, 0f, 0f, 0.6f));
            made.Add(palette);
            var theme = new FakeThemeSource { Current = palette };
            var canvas = scene.GetRootGameObjects().Single(r => r.name == "Canvas").transform;
            foreach (var themed in canvas.GetComponentsInChildren<ThemedGraphic>(true)) themed.Bind(theme);
            return canvas;
        }

        private MenuView OpenMenu(out Transform canvas)
        {
            canvas = Open("Assets/_Project/Scenes/Menu.unity");
            var view = canvas.GetComponentInChildren<MenuView>(true);
            Assert.That(view, Is.Not.Null, "menu view");
            return view;
        }

        private static GameObject Child(Transform parent, string path)
        {
            var found = parent.Find(path);
            Assert.That(found, Is.Not.Null, path);
            return found.gameObject;
        }

        [Test]
        public void Branding_slots_follow_the_test_sprites()
        {
            var logo = MakeSprite("logo", 64, 32);
            var background = MakeSprite("background", 32, 64);

            GameBranding.ForTests(logo, background);

            Assert.That(GameBranding.TitleLogo, Is.SameAs(logo));
            Assert.That(GameBranding.MenuBackground, Is.SameAs(background));
        }

        [Test]
        public void Menu_shows_logo_instead_of_title_text()
        {
            var view = OpenMenu(out var canvas);
            var logo = MakeSprite("logo", 64, 32);

            view.ShowBranding(logo, null);

            var image = Child(view.transform, "TitleLogo").GetComponent<Image>();
            Assert.That(image.gameObject.activeSelf, Is.True, "logo shown");
            Assert.That(image.sprite, Is.SameAs(logo));
            Assert.That(image.preserveAspect, Is.True, "aspect kept");
            Assert.That(image.color, Is.EqualTo(Color.white), "logo in its own colours");
            Assert.That(Child(view.transform, "Title").activeSelf, Is.False, "title text hidden");
            Assert.That(Child(view.transform, "TitleStroke").activeSelf, Is.False, "stroke hidden");
            Assert.That(Child(canvas, "BrandBackdrop").activeSelf, Is.False, "no background");
            Assert.That(Child(canvas, "Decor").activeSelf, Is.True, "decor stays");
        }

        [Test]
        public void Menu_without_logo_keeps_title_text()
        {
            var view = OpenMenu(out var canvas);

            view.ShowBranding(null, null);

            Assert.That(Child(view.transform, "TitleLogo").activeSelf, Is.False, "no logo");
            Assert.That(Child(view.transform, "Title").activeSelf, Is.True, "title text");
            Assert.That(Child(view.transform, "Title").GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(Child(view.transform, "TitleStroke").activeSelf, Is.True, "stroke");
            Assert.That(Child(canvas, "BrandBackdrop").activeSelf, Is.False, "no backdrop");
            Assert.That(Child(canvas, "BrandScrim").activeSelf, Is.False, "no scrim");
            Assert.That(Child(canvas, "Decor").activeSelf, Is.True, "decor");
        }

        // A name that takes two lines at the title's size keeps its Accent stroke under the second line, not across it
        [Test]
        public void Title_stroke_sits_below_a_two_line_title()
        {
            var view = OpenMenu(out var canvas);
            // Laid out at the canvas scaler's 1080 x 1920 reference, whatever size the editor's game view has
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas).sizeDelta = new Vector2(1080f, 1920f);
            Canvas.ForceUpdateCanvases();

            view.SetTitle("Şanslı Çiftlik Macerası");

            var title = view.Title;
            Assert.That(title.textInfo.lineCount, Is.GreaterThanOrEqualTo(2), "the name wraps");
            var textBottom = title.transform.TransformPoint(new Vector3(0f, title.textBounds.min.y, 0f)).y;
            var corners = new Vector3[4];
            view.TitleStroke.GetWorldCorners(corners);
            Assert.That(corners[1].y, Is.LessThan(textBottom), "the stroke's top under the text's last line");
            Assert.That(textBottom - corners[1].y, Is.LessThan(MenuView.StrokeGap * 3f * view.TitleStroke.lossyScale.y), "close under it");
            Assert.That(view.TitleStroke.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Menu_background_replaces_decor()
        {
            var view = OpenMenu(out var canvas);
            var background = MakeSprite("background", 64, 32);

            view.ShowBranding(null, background);

            var backdrop = Child(canvas, "BrandBackdrop");
            Assert.That(backdrop.activeSelf, Is.True, "backdrop shown");
            var image = backdrop.GetComponent<Image>();
            Assert.That(image.sprite, Is.SameAs(background));
            Assert.That(image.color, Is.EqualTo(Color.white), "picture in its own colours");
            var fitter = backdrop.GetComponent<AspectRatioFitter>();
            Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent), "covers the screen");
            Assert.That(fitter.aspectRatio, Is.EqualTo(2f).Within(1e-4f), "the picture's aspect");
            var scrim = Child(canvas, "BrandScrim");
            Assert.That(scrim.activeSelf, Is.True, "scrim shown");
            var themed = scrim.GetComponent<ThemedGraphic>();
            Assert.That(themed.Role, Is.EqualTo(ThemeRole.Scrim));
            Assert.That(themed.Opacity, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(scrim.transform.GetSiblingIndex(), Is.GreaterThan(backdrop.transform.GetSiblingIndex()), "scrim over the picture");
            Assert.That(scrim.transform.GetSiblingIndex(), Is.LessThan(canvas.Find("SafeArea").GetSiblingIndex()), "under the content");
            Assert.That(backdrop.transform.GetSiblingIndex(), Is.GreaterThan(canvas.Find("Background").GetSiblingIndex()), "over the theme");
            Assert.That(Child(canvas, "Decor").activeSelf, Is.False, "decor hidden");
            Assert.That(Child(view.transform, "Title").activeSelf, Is.True, "title text without a logo");
        }

        // The menu's picture: Compass's rendered still, else the skin's menu background, else the theme's decor
        [Test]
        public void Branding_picture_wins_over_the_skin_menu_background()
        {
            var sprites = SkinSprites.MakeV2(made);
            var skin = GameSkin.Parse(SkinSprites.V2, path => sprites.TryGetValue(path, out var sprite) ? sprite : null, _ => null);
            var branding = MakeSprite("branding", 64, 32);
            var skinned = sprites["Art/sample/backgrounds/menu"];
            Assert.That(skin.MenuBackground, Is.SameAs(skinned), "the skin has a menu background");

            Assert.That(MenuView.BackgroundFor(branding, skin), Is.SameAs(branding), "branding first");
            Assert.That(MenuView.BackgroundFor(null, skin), Is.SameAs(skinned), "then the skin");
            Assert.That(MenuView.BackgroundFor(branding, null), Is.SameAs(branding), "branding without a skin");
            Assert.That(MenuView.BackgroundFor(null, null), Is.Null, "neither: the theme's decor");
            Assert.That(MenuView.BackgroundFor(null, GameSkin.Parse(SkinSprites.Json, _ => null, _ => null)), Is.Null, "a v1 skin");
        }

        [Test]
        public void Skin_scrim_replaces_the_brand_scrim()
        {
            var view = OpenMenu(out var canvas);
            var picture = MakeSprite("background", 64, 32);
            Color skinScrim = new Color32(0x14, 0x28, 0x50, 0x47);

            view.ShowBranding(null, picture, skinScrim);

            var scrim = Child(canvas, "BrandScrim");
            Assert.That(scrim.activeSelf, Is.True, "scrim shown");
            Assert.That(scrim.GetComponent<Image>().color, Is.EqualTo(skinScrim), "the skin's scrim");

            // No skin scrim: the Scrim role at 35 % again
            view.ShowBranding(null, picture, null);

            var themed = scrim.GetComponent<ThemedGraphic>();
            Assert.That(themed.Role, Is.EqualTo(ThemeRole.Scrim));
            Assert.That(themed.Opacity, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(scrim.GetComponent<Image>().color, Is.EqualTo(new Color(0f, 0f, 0f, 0.6f * 0.35f)), "the role's colour");

            // The two-argument form keeps the role
            view.ShowBranding(null, picture, skinScrim);
            view.ShowBranding(null, picture);
            Assert.That(scrim.GetComponent<Image>().color, Is.EqualTo(new Color(0f, 0f, 0f, 0.6f * 0.35f)), "two arguments");
        }

        [Test]
        public void Boot_uses_brand_background()
        {
            var canvas = Open("Assets/_Project/Scenes/Boot.unity");
            var view = canvas.GetComponentInChildren<BootBrandView>(true);
            Assert.That(view, Is.Not.Null, "boot brand view");
            var logo = MakeSprite("logo", 64, 32);
            var brand = new Color32(0x12, 0x34, 0x56, 0xFF);

            view.ShowBranding(logo, brand);

            var image = Child(canvas, "SafeArea/Logo").GetComponent<Image>();
            Assert.That(image.gameObject.activeSelf, Is.True, "logo shown");
            Assert.That(image.sprite, Is.SameAs(logo));
            Assert.That(image.preserveAspect, Is.True, "aspect kept");
            Assert.That(image.color, Is.EqualTo(Color.white), "logo in its own colours");
            Assert.That(Child(canvas, "SafeArea/Title").activeSelf, Is.False, "title text hidden");
            var fill = Child(canvas, "BrandFill");
            Assert.That(fill.activeSelf, Is.True, "brand colour shown");
            Assert.That(fill.GetComponent<Image>().color, Is.EqualTo((Color)brand));
            Assert.That(fill.transform.GetSiblingIndex(), Is.GreaterThan(canvas.Find("Background").GetSiblingIndex()), "over the theme");

            // No brand colour: the logo over the theme's background
            view.ShowBranding(logo, null);
            Assert.That(fill.activeSelf, Is.False, "theme background");
            Assert.That(image.gameObject.activeSelf, Is.True, "logo still shown");

            // No logo: today's boot screen, brand colour or not
            view.ShowBranding(null, brand);
            Assert.That(image.gameObject.activeSelf, Is.False, "no logo");
            Assert.That(fill.activeSelf, Is.False, "theme background");
            Assert.That(Child(canvas, "SafeArea/Title").activeSelf, Is.True, "title text");
        }
    }
}
