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
    // The menu's layout as SceneBuilder writes it: the top bar (coins with the shop's mark, settings gear), the bottom
    // block (further games, one row of round feature buttons, the big Play) and the room it leaves for the banner
    public sealed class MenuViewTests
    {
        private readonly List<Object> made = new List<Object>();
        private Scene scene;
        private MenuView view;
        private Transform canvas;

        [SetUp]
        public void Open()
        {
            // A game Compass dressed ships a skin, whose coin icon would stand in for a currency's own disc: these tests
            // start without one either way
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Additive);
            canvas = scene.GetRootGameObjects().Single(r => r.name == "Canvas").transform;
            view = canvas.GetComponentInChildren<MenuView>(true);
            Assert.That(view, Is.Not.Null, "menu view");
        }

        [TearDown]
        public void Close()
        {
            GameSkin.ForTests(null);
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

        // The menu laid out on the 1080 x 1920 reference phone the way a device lays it out (LayoutStage: a world-space
        // canvas in canvas units, every child the builder hid shown)
        private void Stage() => Stage(new Vector2(1080f, 1920f));

        // The menu laid out on a screen of that many pixels (2400 x 1080: a 20:9 phone turned, 2147 x 966 canvas units)
        private void Stage(Vector2 screen)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            LayoutStage.Lay(canvas.gameObject, screen, new FakeThemeSource { Current = palette, Style = ThemeStyle.Load() });
        }

        private void Relayout() => LayoutStage.Layout(canvas.gameObject);

        private RectTransform Play => (RectTransform)view.BottomBlock.Find("Play");

        [Test]
        public void Side_buttons_share_one_row_left_ones_first()
        {
            var icon = MakeSprite("gift");
            var clicks = new List<string>();
            var a = Side(view.AddSideButton(MenuSide.Right, icon, "economy.wheel", () => clicks.Add("a")));
            var b = Side(view.AddSideButton(MenuSide.Left, icon, "economy.gift", () => clicks.Add("b")));
            var c = Side(view.AddSideButton(MenuSide.Left, icon, "economy.login", () => clicks.Add("c")));
            var d = Side(view.AddSideButton(MenuSide.Right, icon, "leaderboards.leaderboard", () => clicks.Add("d")));

            Assert.That(view.SideRow.GetComponent<KitFlowRow>(), Is.Not.Null, "one flow row");
            var order = view.SideRow.Cast<Transform>().Select(t => t.GetComponent<MenuSideButton>()).ToList();
            Assert.That(order, Is.EqualTo(new[] { b, c, a, d }), "the left ones first, each side in the order added");
            foreach (var button in order)
            {
                Assert.That(button.gameObject.activeSelf, Is.True, "shown");
                var caption = button.Caption.GetComponent<TMPro.TMP_Text>();
                Assert.That(caption.GetComponent<ThemedText>().Role, Is.EqualTo(TextRole.Body), $"{button.name} caption in the body font");
                Assert.That(caption.fontStyle & TMPro.FontStyles.Bold, Is.EqualTo(TMPro.FontStyles.Bold), $"{button.name} caption bold");
                Assert.That(button.Button.GetComponent<KitButton>().Variant, Is.EqualTo(KitButtonVariant.Icon), "round button");
                // The caption sits under the circle
                Assert.That(WorldRect((RectTransform)button.Caption.transform).yMax,
                    Is.LessThanOrEqualTo(WorldRect((RectTransform)button.Button.transform).yMin + 0.01f), "caption under the circle");
            }
            Assert.That(b.Caption.Key, Is.EqualTo("economy.gift"), "caption");
            Assert.That(d.Caption.Key, Is.EqualTo("leaderboards.leaderboard"));
            Assert.That(b.Icon.sprite, Is.SameAs(icon), "icon");

            c.Button.onClick.Invoke();
            d.Button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(new[] { "c", "d" }));
        }

        [Test]
        public void A_side_holds_at_most_three_buttons()
        {
            for (var i = 0; i < MenuView.SideSlots; i++) view.AddSideButton(MenuSide.Right, null, "menu.play", null);

            Assert.That(() => view.AddSideButton(MenuSide.Right, null, "menu.play", null), Throws.InvalidOperationException);
            Assert.That(view.AddSideButton(MenuSide.Left, null, "menu.play", null), Is.Not.Null, "the other side has room");
            Assert.That(view.SideRow.childCount, Is.EqualTo(MenuView.SideSlots + 1));
        }

        // A feature that hides its button (gameObject.SetActive(false)) leaves no hole in the row
        [Test]
        public void A_hidden_side_button_leaves_no_gap()
        {
            Stage();
            var first = Side(view.AddSideButton(MenuSide.Left, null, "economy.gift", null));
            var middle = Side(view.AddSideButton(MenuSide.Left, null, "economy.login", null));
            var last = Side(view.AddSideButton(MenuSide.Right, null, "economy.wheel", null));
            middle.gameObject.SetActive(false);
            Relayout();

            var flow = view.SideRow.GetComponent<KitFlowRow>();
            var left = WorldRect((RectTransform)first.transform);
            var right = WorldRect((RectTransform)last.transform);
            Assert.That(right.xMin - left.xMax, Is.EqualTo(flow.Gap * view.SideRow.lossyScale.x).Within(0.01f), "one gap apart");
            Assert.That((left.xMin + right.xMax) / 2f, Is.EqualTo(WorldRect(view.SideRow).center.x).Within(0.5f), "centred in the row");
        }

        // The row's captions share one size (KitTextGroup), the largest at which the longest fits its box; one line each
        [Test]
        public void Side_captions_share_one_size()
        {
            Stage();
            var short_ = Side(view.AddSideButton(MenuSide.Left, null, "economy.wheel", null));
            var hidden = Side(view.AddSideButton(MenuSide.Left, null, "economy.gift", null));
            var long_ = Side(view.AddSideButton(MenuSide.Right, null, "leaderboards.achievements", null));
            short_.Caption.GetComponent<TMPro.TMP_Text>().text = "Spin";
            hidden.Caption.GetComponent<TMPro.TMP_Text>().text = "Supercalifragilistic";
            long_.Caption.GetComponent<TMPro.TMP_Text>().text = "Achievements";
            hidden.gameObject.SetActive(false);
            Relayout();

            var group = view.SideRow.GetComponent<KitTextGroup>();
            Assert.That(group, Is.Not.Null, "the captions' group");
            var shortText = short_.Caption.GetComponent<TMPro.TMP_Text>();
            var longText = long_.Caption.GetComponent<TMPro.TMP_Text>();
            Assert.That(longText.fontSize, Is.LessThan(32f), "the long caption shrinks");
            Assert.That(longText.fontSize, Is.GreaterThanOrEqualTo(22f), "not under its minimum");
            Assert.That(shortText.fontSize, Is.EqualTo(longText.fontSize), "the short one takes the same size");
            Assert.That(longText.GetPreferredValues(longText.text).x, Is.LessThanOrEqualTo(longText.rectTransform.rect.width + 0.01f), "fits its box");
            Assert.That(longText.enableAutoSizing, Is.False, "sized by the group, not on its own");
            Assert.That(longText.textWrappingMode, Is.EqualTo(TMPro.TextWrappingModes.NoWrap), "one line");
        }

        [Test]
        public void Side_row_sits_right_above_play()
        {
            Stage();
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            Relayout();

            var row = WorldRect(view.SideRow);
            var play = WorldRect(Play);
            var menu = WorldRect((RectTransform)view.transform);
            var scale = view.SideRow.lossyScale.y;
            Assert.That(row.yMin, Is.EqualTo(play.yMax + MenuView.BlockGap * scale).Within(1f), "BlockGap above Play");
            Assert.That(row.center.x, Is.EqualTo(menu.center.x).Within(1f), "row centred");
            Assert.That(play.center.x, Is.EqualTo(menu.center.x).Within(1f), "Play centred");
            Assert.That(play.yMin, Is.EqualTo(menu.yMin + MenuView.BottomInset * scale).Within(1f), "Play BottomInset above the bottom");
        }

        [Test]
        public void Play_is_70_percent_of_the_reference_width()
        {
            Stage();

            Assert.That(MenuView.PlaySize.x, Is.EqualTo(0.7f * 1080f).Within(0.01f), "70 % of the 1080 reference");
            Assert.That(Play.rect.size, Is.EqualTo(new Vector2(756f, 230f)));
            Assert.That(Play.GetComponent<KitButton>().Variant, Is.EqualTo(KitButtonVariant.Play), "the Play variant (Accent, raised)");
            Assert.That(Play.GetComponent<IdlePulse>(), Is.Not.Null, "slow pulse");
        }

        // Side by side: stacked, three games' buttons would reach the logo on a portrait tablet
        [Test]
        public void Further_games_sit_side_by_side_above_the_side_row()
        {
            Stage();
            view.AddSideButton(MenuSide.Left, null, "economy.gift", null);
            view.AddSideButton(MenuSide.Right, null, "economy.wheel", null);
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole))) palette.Set(role, Color.gray);
            made.Add(palette);
            view.ShowGames(new[] { "menu.play", "other.play", "third.play" },
                new UiBinder(new NullLocalizer(), new FakeThemeSource { Current = palette }));
            Relayout();

            var buttons = view.GameButtons.Select(b => (RectTransform)b.transform).ToList();
            Assert.That(buttons.Count, Is.EqualTo(3));
            Assert.That(buttons[0], Is.SameAs(Play), "the first game on the big Play");
            var row = WorldRect(view.SideRow);
            var scale = view.SideRow.lossyScale.y;
            foreach (var copy in buttons.Skip(1))
            {
                Assert.That(copy.rect.size, Is.EqualTo(MenuView.GameCopySize), $"{copy.name} smaller");
                Assert.That(WorldRect(copy).yMin, Is.EqualTo(row.yMax + MenuView.BlockGap * scale).Within(1f), $"{copy.name} right above the row");
            }
            var second = WorldRect(buttons[1]);
            var third = WorldRect(buttons[2]);
            Assert.That(third.xMin - second.xMax, Is.EqualTo(MenuView.GameGap * scale).Within(0.5f), "the second on the left, GameGap apart");
            Assert.That((second.xMin + third.xMax) / 2f, Is.EqualTo(WorldRect((RectTransform)view.transform).center.x).Within(1f), "centred");
            Assert.That(row.yMin, Is.EqualTo(WorldRect(Play).yMax + MenuView.BlockGap * scale).Within(1f), "the row still right above Play");
        }

        [Test]
        public void Coins_pill_is_as_tall_as_settings()
        {
            var pill = (RectTransform)view.Coins.transform;
            var settings = (RectTransform)view.SettingsButton.transform;
            Assert.That(pill.rect.height, Is.EqualTo(settings.rect.height).Within(0.01f));
            Assert.That(settings.rect.height, Is.EqualTo(120f).Within(0.01f));
        }

        // The shop's "+" at the coins pill's right end, only while the shop can show (EconomyMenu)
        [Test]
        public void Buy_mark_shows_only_with_a_shop()
        {
            Assert.That(view.BuyMark, Is.Not.Null);
            Assert.That(view.BuyMark.transform.parent, Is.SameAs(view.Coins.transform), "on the pill");
            Assert.That(view.BuyMark.activeSelf, Is.False, "hidden as built");
            view.SetCoins(10, new CurrencyMark(null, Color.yellow, "C"));

            view.ShowBuy(true);
            Assert.That(view.BuyMark.activeSelf, Is.True, "shown with a shop");
            var pill = WorldRect((RectTransform)view.Coins.transform);
            var mark = WorldRect((RectTransform)view.BuyMark.transform);
            Assert.That(mark.xMax, Is.LessThanOrEqualTo(pill.xMax + 0.01f), "inside the pill");
            Assert.That(pill.xMax - mark.xMax, Is.LessThan(pill.height / 4f), "at its right end");
            Assert.That(mark.center.y, Is.EqualTo(pill.center.y).Within(0.01f), "centred on its height");
            Assert.That(view.BuyMark.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Accent), "an Accent mark");

            view.ShowBuy(false);
            Assert.That(view.BuyMark.activeSelf, Is.False, "hidden without a shop");

            view.ShowBuy(true);
            view.SetCoins(null, null);
            Assert.That(view.BuyMark.activeSelf, Is.False, "hidden with the counter");
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

        // Turned to landscape: the title and the logo centred at (0.27, 0.55) of the view, at most 46 % of its width; the
        // bottom block in the right half, 60 units from the right edge and LandscapeBottomInset above the bottom
        [Test]
        public void Landscape_puts_the_logo_left_and_the_block_bottom_right()
        {
            Stage(new Vector2(2400f, 1080f));
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            view.SetTitle("Şanslı Çiftlik Macerası");
            view.Arrange();
            Relayout();

            var menu = WorldRect((RectTransform)view.transform);
            Assert.That(menu.size.x, Is.EqualTo(2147f).Within(1f), "a 20:9 landscape view");
            Assert.That(menu.size.y, Is.EqualTo(966f).Within(1f));
            foreach (var band in new[] { view.Title.rectTransform, view.TitleLogo.rectTransform })
            {
                var rect = WorldRect(band);
                Assert.That(rect.center.x, Is.LessThan(menu.center.x), $"{band.name} in the left half");
                Assert.That(rect.center.x, Is.EqualTo(menu.xMin + MenuView.LandscapeTitleX * menu.width).Within(1f), $"{band.name} centred at 0.27");
                Assert.That(rect.center.y, Is.EqualTo(menu.yMin + MenuView.LandscapeTitleY * menu.height).Within(1f), $"{band.name} centred at 0.55");
                Assert.That(rect.width, Is.LessThanOrEqualTo(0.46f * menu.width + 0.5f), $"{band.name} at most 46 % of the width");
            }
            var block = WorldRect(view.BottomBlock);
            Assert.That(block.xMin, Is.GreaterThanOrEqualTo(menu.center.x - 0.5f), "the block in the right half");
            Assert.That(block.xMax, Is.EqualTo(menu.xMax - MenuView.LandscapeSideInset).Within(1f), "60 units from the right edge");
            Assert.That(block.yMin, Is.EqualTo(menu.yMin + MenuView.LandscapeBottomInset).Within(1f), "48 units above the bottom");
            Assert.That(MenuView.LandscapeBottomInset, Is.EqualTo(48f));
            Assert.That(block.yMax, Is.LessThanOrEqualTo(WorldRect(view.TopBar).yMin), "under the top bar");
            var stroke = WorldRect(view.TitleStroke);
            Assert.That(stroke.center.x, Is.EqualTo(WorldRect(view.Title.rectTransform).center.x).Within(1f), "the stroke follows the title");
            Assert.That(stroke.yMax, Is.LessThan(WorldRect(view.Title.rectTransform).yMax), "under the title");
        }

        // A rotation while the menu shows: portrait, landscape and portrait again end where the builder put everything
        // (the further games and the features' rows back in the bottom block, in their places)
        [Test]
        public void Turning_back_to_portrait_restores_the_built_layout()
        {
            Stage();
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole))) palette.Set(role, Color.gray);
            made.Add(palette);
            view.ShowGames(new[] { "menu.play", "other.play", "third.play" },
                new UiBinder(new NullLocalizer(), new FakeThemeSource { Current = palette }));
            var row = new GameObject("Row", typeof(RectTransform));
            row.AddComponent<LayoutElement>().preferredHeight = 70f;
            row.transform.SetParent(view.ExtraSlot, false);
            view.SetTitle("Şanslı Çiftlik Macerası");
            Relayout();
            var built = Snapshot();
            Assert.That(view.transform.Find("Column"), Is.Null, "no landscape column as built");

            view.Arrange();
            Relayout();
            Assert.That(Snapshot(), Is.EqualTo(built), "arranging in portrait keeps the built layout");

            Stage(new Vector2(2400f, 1080f));
            view.Arrange();
            Relayout();
            var landscape = Snapshot();
            Assert.That(landscape, Is.Not.EqualTo(built), "landscape moves the parts");
            Assert.That(view.BottomBlock.anchorMin.x, Is.EqualTo(0.5f), "the block in the right half");
            Assert.That(view.ExtraSlot.IsChildOf(view.BottomBlock), Is.False, "the features' rows left of it");
            var column = view.transform.Find("Column");
            Assert.That(column != null && column.gameObject.activeSelf, Is.True, "the landscape column shown");
            view.Arrange();
            Relayout();
            Assert.That(Snapshot(), Is.EqualTo(landscape), "arranging again changes nothing");

            Stage();
            view.Arrange();
            Relayout();
            Assert.That(Snapshot(), Is.EqualTo(built), "back to portrait: as built");
            Assert.That(column.gameObject.activeSelf, Is.False, "the landscape column hidden in portrait");
        }

        // A landscape view too short for the block at full size: the block scales down as a whole, but never so far that
        // a round button's circle or Play drops under the touch size; at that floor it comes down to the kit's edge gap
        [Test]
        public void Short_landscape_scales_the_block_down_to_the_touch_size_only()
        {
            Stage(new Vector2(1920f, 1080f));
            var sides = new List<MenuSideButton>();
            for (var i = 0; i < 3; i++) sides.Add(Side(view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null)));
            // 655 units: room for the block at 0.99; 560: at the touch floor (130-unit circles to 126)
            foreach (var height in new[] { 655f, 560f })
            {
                ((RectTransform)canvas).sizeDelta = new Vector2(1920f, height);
                Relayout();
                view.Arrange();
                Relayout();

                var menu = WorldRect((RectTransform)view.transform);
                Assert.That(menu.height, Is.EqualTo(height).Within(0.5f), "a short landscape view");
                var scale = view.BottomBlock.localScale.x;
                Assert.That(scale, Is.GreaterThan(0f).And.LessThan(1f), $"{height}: the block scaled down");
                foreach (var side in sides)
                    Assert.That(WorldRect((RectTransform)side.Button.transform).height, Is.GreaterThanOrEqualTo(KitLayout.MinTouch - 0.01f),
                        $"{height}: {side.name} circle touch-sized");
                Assert.That(WorldRect(Play).height, Is.GreaterThanOrEqualTo(KitLayout.MinTouch), $"{height}: Play touch-sized");
                var block = WorldRect(view.BottomBlock);
                if (height > 600f)
                {
                    Assert.That(block.yMin - menu.yMin, Is.EqualTo(MenuView.LandscapeBottomInset).Within(0.5f), "48 above the bottom");
                    Assert.That(block.yMax, Is.LessThanOrEqualTo(WorldRect(view.TopBar).yMin - MenuView.BlockGap + 0.5f), "under the top bar");
                }
                else
                {
                    Assert.That(WorldRect((RectTransform)sides[0].Button.transform).height, Is.EqualTo(KitLayout.MinTouch).Within(0.5f), "at the floor");
                    Assert.That(block.yMin - menu.yMin, Is.EqualTo(KitLayout.Gap).Within(0.5f), "down to the kit's edge gap");
                }
            }
        }

        // A feature's row in the bottom block's extra slot, height units high; with a button filling it (a Selectable)
        private RectTransform ExtraRow(float height, bool button = false)
        {
            var row = new GameObject($"Row{view.ExtraSlot.childCount}", typeof(RectTransform));
            row.AddComponent<LayoutElement>().preferredHeight = height;
            row.transform.SetParent(view.ExtraSlot, false);
            if (button)
            {
                var hit = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)hit.transform;
                rect.SetParent(row.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
            }
            return (RectTransform)row.transform;
        }

        // The title (with its stroke under it) and the logo between the bottom block and the top bar, BlockGap from both
        private void AssertBandsBetweenBlockAndBar(string where)
        {
            var above = WorldRect(view.BottomBlock).yMax + MenuView.BlockGap;
            var top = WorldRect(view.TopBar).yMin - MenuView.BlockGap;
            foreach (var band in new[] { view.Title.rectTransform, view.TitleStroke, view.TitleLogo.rectTransform })
            {
                var rect = WorldRect(band);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(above - 0.5f), $"{where}: {band.name} {rect} over the block (+gap) {above}");
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(top + 0.5f), $"{where}: {band.name} {rect} under the top bar (-gap) {top}");
            }
        }

        // A block busy enough to reach the title's band with no banner: the band moves down between the block and the top
        // bar (at full size while it has the room), the features' rows stay at full size
        [Test]
        public void A_busy_block_moves_the_title_down_under_the_top_bar()
        {
            Stage();
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            var row = ExtraRow(600f);
            view.SetTitle("Şanslı Çiftlik Macerası");
            Relayout();
            var built = WorldRect(view.Title.rectTransform);
            Assert.That(built.yMin - MenuView.StrokeGap - view.TitleStroke.rect.height,
                Is.LessThan(WorldRect(view.BottomBlock).yMax + MenuView.BlockGap), "as built, the block reaches the title");

            view.Arrange();
            Relayout();

            AssertBandsBetweenBlockAndBar("moved");
            Assert.That(view.Title.rectTransform.localScale.x, Is.EqualTo(1f).Within(0.0001f), "room for the title at full size");
            Assert.That(WorldRect(view.Title.rectTransform).yMin, Is.GreaterThan(built.yMin), "moved up off the block");
            Assert.That(row.localScale, Is.EqualTo(Vector3.one), "the features' rows at full size");
        }

        // No room for the band even at its smallest: the features' rows scale down together until it fits
        [Test]
        public void Rows_scale_down_when_the_title_cannot_fit_at_its_smallest()
        {
            Stage();
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            var rows = new[] { ExtraRow(600f), ExtraRow(400f) };
            view.SetTitle("Şanslı Çiftlik Macerası");
            Relayout();
            var room = WorldRect(view.TopBar).yMin - MenuView.BlockGap - (WorldRect(view.BottomBlock).yMax + MenuView.BlockGap);
            Assert.That(room, Is.LessThan(0.3f * view.Title.rectTransform.rect.height + MenuView.StrokeGap + view.TitleStroke.rect.height),
                "too little room for the title at its smallest");

            view.Arrange();
            Relayout();

            var scale = rows[0].localScale.x;
            Assert.That(scale, Is.LessThan(1f).And.GreaterThan(0.9f), "the rows scaled down");
            Assert.That(rows[1].localScale.x, Is.EqualTo(scale).Within(0.0001f), "together");
            Assert.That(WorldRect(rows[0]).height, Is.EqualTo(600f * scale).Within(0.5f), "taking their scaled height in the block");
            AssertBandsBetweenBlockAndBar("rows scaled");
        }

        // The rows never scale a button among them under the touch size
        [Test]
        public void Rows_keep_their_buttons_touch_sized()
        {
            Stage();
            for (var i = 0; i < 3; i++) view.AddSideButton(i == 1 ? MenuSide.Right : MenuSide.Left, null, "economy.gift", null);
            ExtraRow(600f);
            var row = ExtraRow(130f, true);
            ExtraRow(400f);
            view.SetTitle("Şanslı Çiftlik Macerası");
            view.Arrange();
            Relayout();

            Assert.That(row.localScale.x, Is.EqualTo(KitLayout.MinTouch / 130f).Within(0.0001f), "down to the button's touch floor");
            Assert.That(WorldRect((RectTransform)row.Find("Button")).height, Is.GreaterThanOrEqualTo(KitLayout.MinTouch - 0.01f), "touch-sized");
        }

        // The rects Arrange moves: anchors, pivot, position, size (the block's height is its content's), scale, parent and
        // place among the siblings
        private List<string> Snapshot()
        {
            var parts = new[]
            {
                view.Title.rectTransform, view.TitleLogo.rectTransform, view.TitleStroke, view.BottomBlock, view.Version.rectTransform,
                (RectTransform)view.ExtraSlot, (RectTransform)view.GameButtons[1].transform.parent,
                view.SideRow, Play
            };
            return parts.Select(r =>
                $"{r.name}: {r.anchorMin:F3} {r.anchorMax:F3} {r.pivot:F3} {r.anchoredPosition:F2} {r.sizeDelta:F2} {r.localScale:F3} " +
                $"in {r.parent.name} at {r.GetSiblingIndex()}").ToList();
        }

        private sealed class NullLocalizer : Kturker.UnityBase.Core.Domain.ILocalizer
        {
            public string Language => "en";
            public event Action Changed { add { } remove { } }
            public string Get(string key, params object[] args) => key;
        }
    }
}
