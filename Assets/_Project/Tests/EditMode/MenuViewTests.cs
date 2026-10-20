using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The menu's layout as SceneBuilder writes it: the top bar (coins, settings gear), the side columns of round feature
    // buttons and the room it leaves for the banner
    public sealed class MenuViewTests
    {
        private readonly List<Object> made = new List<Object>();
        private Scene scene;
        private MenuView view;
        private Transform canvas;

        [SetUp]
        public void Open()
        {
            scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Additive);
            canvas = scene.GetRootGameObjects().Single(r => r.name == "Canvas").transform;
            view = canvas.GetComponentInChildren<MenuView>(true);
            Assert.That(view, Is.Not.Null, "menu view");
        }

        [TearDown]
        public void Close()
        {
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(8, 8) { name = name };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static MenuSideButton Side(IMenuSideButton button) => (MenuSideButton)button;

        [Test]
        public void Side_buttons_stack_on_their_side_with_labels()
        {
            var icon = MakeSprite("gift");
            var clicks = new List<string>();
            var gift = Side(view.AddSideButton(MenuSide.Left, icon, "economy.gift", () => clicks.Add("gift")));
            var wheel = Side(view.AddSideButton(MenuSide.Left, icon, "economy.wheel", () => clicks.Add("wheel")));
            var board = Side(view.AddSideButton(MenuSide.Right, icon, "leaderboards.leaderboard", () => clicks.Add("board")));
            foreach (var group in canvas.GetComponentsInChildren<LayoutGroup>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);

            var menu = (RectTransform)view.transform;
            var middle = WorldRect(menu).center.x;
            Assert.That(gift.transform.parent.name, Is.EqualTo("LeftSide"));
            Assert.That(wheel.transform.parent, Is.SameAs(gift.transform.parent));
            Assert.That(board.transform.parent.name, Is.EqualTo("RightSide"));
            Assert.That(WorldRect((RectTransform)gift.transform).xMax, Is.LessThan(middle), "left column on the left");
            Assert.That(WorldRect((RectTransform)board.transform).xMin, Is.GreaterThan(middle), "right column on the right");
            Assert.That(WorldRect((RectTransform)wheel.transform).yMax, Is.LessThanOrEqualTo(WorldRect((RectTransform)gift.transform).yMin + 0.01f),
                "the second sits under the first");
            Assert.That(gift.gameObject.activeSelf, Is.True, "shown");
            Assert.That(gift.Caption.Key, Is.EqualTo("economy.gift"), "caption");
            Assert.That(board.Caption.Key, Is.EqualTo("leaderboards.leaderboard"));
            Assert.That(gift.Icon.sprite, Is.SameAs(icon), "icon");
            Assert.That(gift.Button.GetComponent<KitButton>().Variant, Is.EqualTo(KitButtonVariant.Icon), "round button");
            // The caption sits under the circle
            Assert.That(WorldRect((RectTransform)gift.Caption.transform).yMax,
                Is.LessThanOrEqualTo(WorldRect((RectTransform)gift.Button.transform).yMin + 0.01f), "caption under the circle");

            wheel.Button.onClick.Invoke();
            board.Button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(new[] { "wheel", "board" }));
        }

        [Test]
        public void A_side_holds_at_most_three_buttons()
        {
            for (var i = 0; i < MenuView.SideSlots; i++) view.AddSideButton(MenuSide.Right, null, "menu.play", null);

            Assert.That(() => view.AddSideButton(MenuSide.Right, null, "menu.play", null), Throws.InvalidOperationException);
            Assert.That(view.AddSideButton(MenuSide.Left, null, "menu.play", null), Is.Not.Null, "the other side has room");
            Assert.That(canvas.Find("SafeArea/Menu/RightSide").childCount, Is.EqualTo(MenuView.SideSlots));
        }

        [Test]
        public void Side_button_badges_show_dot_timer_and_ad()
        {
            var button = Side(view.AddSideButton(MenuSide.Left, null, "economy.gift", null));
            Assert.That(button.Badge, Is.EqualTo(MenuBadge.None), "no badge at first");
            Assert.That(button.Dot.activeSelf || button.Timer.activeSelf || button.Ad.activeSelf, Is.False);

            button.SetBadge(MenuBadge.Dot);
            Assert.That((button.Dot.activeSelf, button.Timer.activeSelf, button.Ad.activeSelf), Is.EqualTo((true, false, false)), "dot");

            button.SetBadge(MenuBadge.Timer, "2:59:41");
            Assert.That((button.Dot.activeSelf, button.Timer.activeSelf, button.Ad.activeSelf), Is.EqualTo((false, true, false)), "timer");
            Assert.That(button.TimerText.text, Is.EqualTo("2:59:41"));

            button.SetBadge(MenuBadge.Ad);
            Assert.That((button.Dot.activeSelf, button.Timer.activeSelf, button.Ad.activeSelf), Is.EqualTo((false, false, true)), "ad");
            Assert.That(button.Ad.GetComponentsInChildren<Image>(true).Any(i => i.sprite == KitSprites.Video), Is.True, "video mark");

            button.SetBadge(MenuBadge.None);
            Assert.That(button.Dot.activeSelf || button.Timer.activeSelf || button.Ad.activeSelf, Is.False, "none");
        }

        [Test]
        public void Coins_hidden_until_set()
        {
            Assert.That(view.Coins.activeSelf, Is.False, "hidden at first");
            Assert.That(view.Coins.transform.parent, Is.SameAs(view.TopBar), "in the top bar");
            var settings = (RectTransform)view.SettingsButton.transform;
            Assert.That(WorldRect((RectTransform)view.Coins.transform).center.x, Is.LessThan(WorldRect(settings).center.x),
                "coins on the left, the gear on the right");
            Assert.That(settings.parent, Is.SameAs(view.TopBar));
            var coin = MakeSprite("coin");

            var blue = new Color(0.2f, 0.6f, 0.95f);
            view.SetCoins(1250, new CurrencyMark(coin, blue, "G"));
            Assert.That(view.Coins.activeSelf, Is.True);
            Assert.That(view.CoinCount.text, Is.EqualTo("1250"));
            Assert.That(view.CoinIcon.Glyph.sprite, Is.SameAs(coin));
            Assert.That(view.CoinIcon.Glyph.color, Is.EqualTo(blue), "the currency's own colour, not the theme's");
            Assert.That(view.CoinIcon.Glyph.GetComponent<Outline>().enabled, Is.True, "a darker rim");

            // No icon: the disc in its colour with its initial
            view.SetCoins(7, new CurrencyMark(null, blue, "G"));
            Assert.That((view.CoinIcon.UsesFallback, view.CoinIcon.Disc.color), Is.EqualTo((true, blue)));
            Assert.That(view.CoinIcon.Letter.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("G"));

            view.SetCoins(null, null);
            Assert.That(view.Coins.activeSelf, Is.False, "hidden again");
        }

        // SafeArea already keeps the content below the banner; the menu adds one button height above its top bar only
        // while the banner shows
        [Test]
        public void Banner_space_is_reserved_only_when_shown()
        {
            var bar = view.TopBar;
            var button = ((RectTransform)view.SettingsButton.transform).rect.height;
            Assert.That(bar.anchoredPosition.y, Is.EqualTo(-MenuView.TopInset).Within(0.01f), "no gap as built");

            view.ReserveBannerSpace(true);
            Assert.That(bar.anchoredPosition.y, Is.EqualTo(-(MenuView.TopInset + button)).Within(0.01f), "one button height lower");
            Assert.That(button, Is.GreaterThan(0f));

            view.ReserveBannerSpace(false);
            Assert.That(bar.anchoredPosition.y, Is.EqualTo(-MenuView.TopInset).Within(0.01f), "back without the banner");
        }

        // About and Settings left the menu's middle: the gear is the top bar's, About the settings panel's last row
        [Test]
        public void Settings_and_about_leave_the_middle_of_the_menu()
        {
            var menu = canvas.Find("SafeArea/Menu");
            Assert.That(menu.Find("Settings"), Is.Null);
            Assert.That(menu.Find("About"), Is.Null);
            Assert.That(menu.Find("TopBar/Settings"), Is.Not.Null);
            Assert.That(menu.Find("SideButton").gameObject.activeSelf, Is.False, "the template stays hidden");
        }

        [Test]
        public void Further_games_stack_above_play_smaller()
        {
            var play = (RectTransform)menuPlay().transform;
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole))) palette.Set(role, Color.gray);
            made.Add(palette);
            view.ShowGames(new[] { "menu.play", "other.play", "third.play" },
                new UiBinder(new NullLocalizer(), new FakeThemeSource { Current = palette }));

            var buttons = view.GameButtons.Select(b => (RectTransform)b.transform).ToList();
            Assert.That(buttons[0], Is.SameAs(play));
            Assert.That(buttons[1].sizeDelta, Is.EqualTo(MenuView.GameCopySize));
            Assert.That(WorldRect(buttons[1]).yMin, Is.GreaterThan(WorldRect(buttons[0]).yMax), "above Play");
            Assert.That(WorldRect(buttons[2]).yMin, Is.GreaterThan(WorldRect(buttons[1]).yMax), "above the second");
        }

        private Button menuPlay() => canvas.Find("SafeArea/Menu/Play").GetComponent<Button>();

        private sealed class NullLocalizer : Kturker.UnityBase.Core.Domain.ILocalizer
        {
            public string Language => "en";
            public event Action Changed { add { } remove { } }
            public string Get(string key, params object[] args) => key;
        }
    }
}
