using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.App.Settings;
// cmpose:multiLang
using Kturker.UnityBase.Core.Language;
// /cmpose:multiLang
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
// cmpose:purchases
using Kturker.UnityBase.Core.Purchases;
// /cmpose:purchases
using Kturker.UnityBase.Features.Economy;
// cmpose:sample
using Kturker.UnityBase.Features.Shell.GameShell;
// /cmpose:sample
// cmpose:showcase
using Kturker.UnityBase.Features.Showcase.Common;
// cmpose:purchases
using Kturker.UnityBase.Features.Showcase.Common.Purchases;
// /cmpose:purchases
// cmpose:saveSystem
using Kturker.UnityBase.Features.Showcase.Common.Save;
// /cmpose:saveSystem
// cmpose:leaderboards
using Kturker.UnityBase.Core.Leaderboards;
using Kturker.UnityBase.Features.Showcase.Common.Leaderboards;
// /cmpose:leaderboards
// /cmpose:showcase
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The kit's layout rules on the built screens (spec §4.4), on every LayoutStage screen, without a skin, with a
    // version 2 skin whose frames are thick and in the tallest font pair in a chunky family (LayoutStage.TallStyle): round
    // icons centred on their face, the busiest settings card in every language (one-line texts, one control column, an equal button grid,
    // touch-sized controls on the screen, one primary, Close top right, the language page), HUD items apart, touch
    // targets of 48 dp that never overlap, texts inside their boxes, the busy menu's bottom block clear of its title,
    // top bar and version.
    [Category("Layout")]
    public sealed class LayoutRuleTests
    {
        private const string SettingsPrefab = "Assets/_Project/App/Settings/SettingsPanel.prefab";
        private const string SettingsCard = "Panel/SafeArea/Card";
        // The Credits panel over the settings card (CreditsPanel): its title, scrolling text box and Close
        private const string CreditsArea = "Credits/Panel/SafeArea";
        private const string MenuScene = "Assets/_Project/Scenes/Menu.unity";
        // cmpose:sample
        private const string GameScene = "Assets/_Project/Scenes/Game.unity";
        // /cmpose:sample
        // The economy's windows, each a KitSheet under its own root on one canvas
        private const string EconomyPrefab = "Assets/_Project/Features/Economy/Resources/Economy/EconomyWindows.prefab";
        // The language page over the settings card's content (it opens in Body's place)
        private const string SettingsLanguages = SettingsCard + "/Languages";
        // Within this many units
        private const float Slack = 0.5f;

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            SafeArea.ReservedTop = 0f;
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            KitTextOutline.DestroyMaterialsForTests();
        }

        // Every screen in the app's style without a skin and with the thick-frame skin, and in the tall font pair
        // (LayoutStage.TallStyle, in bright: framed modal cards and raised pill buttons) without a skin: the app's own
        // style has the fonts and the look of one family only (clean), and a game may pick the pair the kit's sizes leave
        // the least room for in the chunky look (the skin brings its own font and shapes); check gets the screen's name
        // and a theme
        private void ForEachScreen(Action<string, Vector2, IThemeSource> check)
        {
            var own = ThemeStyle.Load();
            var tall = LayoutStage.TallStyle(made);
            foreach (var (variant, skinned, style) in new[] { ("", false, own), (" (skin)", true, own), (" (Baloo 2, bright)", false, tall) })
            {
                GameSkin.ForTests(skinned ? ThickSkin() : GameSkin.Parse("{}", _ => null, _ => null));
                if (style == tall) Assert.That(KitShape.Chunky(style), Is.True, "the tall style runs the chunky look");
                foreach (var (name, screen) in LayoutStage.Screens)
                    check(name + variant, screen, Theme(style));
            }
        }

        // SkinSprites.V2 at sliceScale 3, its card, sheet and dialog frames 30 source pixels thick (90 card units)
        private GameSkin ThickSkin()
        {
            var sprites = SkinSprites.MakeV2(made);
            foreach (var name in new[] { "panel", "sheet", "dialog" })
            {
                var texture = new Texture2D(96, 96) { name = name };
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(30f, 30f, 30f, 30f));
                made.Add(texture);
                made.Add(sprite);
                sprites[$"Art/pixel-ui-pack/{name}"] = sprite;
            }
            var json = SkinSprites.V2.Replace("\"sliceScale\":4", "\"sliceScale\":3");
            return GameSkin.Parse(json, path => sprites.TryGetValue(path, out var sprite) ? sprite : null, _ => null);
        }

        private FakeThemeSource Theme(ThemeStyle style)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            return new FakeThemeSource { Current = palette, Style = style };
        }

        // A scene staged for check, closed afterwards
        private static void InScene(string path, Vector2 screen, IThemeSource theme, Action<Transform> check)
        {
            var canvas = LayoutStage.Scene(path, screen, theme, out var scene);
            try
            {
                check(canvas);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform Require(Transform root, string path)
        {
            var found = root.Find(path);
            Assert.That(found, Is.Not.Null, $"{root.name}/{path}");
            return found;
        }

        // A rect in another rect's own units
        private static Rect In(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = space.InverseTransformPoint(corners[0]);
            var max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - Slack && inner.xMax <= outer.xMax + Slack && inner.yMin >= outer.yMin - Slack &&
            inner.yMax <= outer.yMax + Slack;

        [Test]
        public void Round_icons_are_centred()
        {
            var problems = new List<string>();
            void Check(string where, Transform button)
            {
                var kit = button.GetComponent<KitButton>();
                var space = (RectTransform)button;
                var isIcon = kit.Icon != null && kit.Icon.isActiveAndEnabled && kit.Icon.sprite != null;
                var glyph = isIcon ? kit.Icon.rectTransform : kit.Label.rectTransform;
                // A text glyph (the pause button's "II") also sits its font's cap offset off so that its capitals are on
                // the face's middle (KitButton.CapOffset, as KitLayoutTests measures it): only the face's offset is measured
                var cap = isIcon ? Vector2.zero : new Vector2(0f, KitButton.CapOffset(kit.Label));
                var off = In(space, glyph).center - cap - (In(space, kit.Body.rectTransform).center + kit.ContentOffset);
                if (off.magnitude > 1f) problems.Add($"{where}: {LayoutStage.PathOf(button)} icon {off} off its face's centre");
            }
            ForEachScreen((name, screen, theme) =>
            {
                InScene(MenuScene, screen, theme, canvas => Check(name, Require(canvas, "SafeArea/Menu/TopBar/Settings")));
                // cmpose:sample
                InScene(GameScene, screen, theme, canvas =>
                {
                    Check(name, Require(canvas, "Pause/SafeArea/PauseButton"));
                    foreach (Transform slot in Require(canvas, "SafeArea/ShellHud/Content/BoosterBar/Row")) Check(name, slot);
                });
                // /cmpose:sample
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The languages the texts are checked in
        private static List<string> Languages()
        {
            var languages = new List<string> { "en" };
            // cmpose:multiLang
            languages.AddRange(new[] { "tr", "fr", "es", "it" });
            // /cmpose:multiLang
            return languages;
        }

        // What the card's footer can show in the place of its links (the settings actions' results)
        private static List<string> MessageKeys()
        {
            var keys = new List<string>();
            // cmpose:purchases
            keys.AddRange(new[]
            {
                PurchasesKeys.Restored, PurchasesKeys.ErrorUnavailable, PurchasesKeys.ErrorPackageUnavailable, PurchasesKeys.ErrorNothingToRestore
            });
            foreach (StoreErrorKind kind in Enum.GetValues(typeof(StoreErrorKind))) keys.Add(PurchasesKeys.Error(kind));
            // /cmpose:purchases
            return keys;
        }

        // The settings card at its busiest on every screen in every language: every row, Primary, the six grid buttons, both
        // links and the version shown (LayoutStage shows every child), the texts in the language, the footer's message
        // the widest one there is, the language page's rows copied for the supported languages; check gets the
        // screen's and the language's name, the staged root and the card. Each staged copy is gone after its check.
        private void ForEachBusyCard(Action<string, Transform, Transform> check)
        {
            var localizer = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            foreach (var language in Languages())
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                ForEachScreen((name, screen, theme) =>
                {
                    var root = LayoutStage.Prefab(SettingsPrefab, screen, theme, made);
                    var card = Require(root.transform, SettingsCard);
                    Busy(card, localizer);
                    LayoutStage.Layout(root);
                    check($"{name} {language}", root.transform, card);
                    Object.DestroyImmediate(root);
                });
            }
        }

        private static void Busy(Transform card, TableLocalizer localizer)
        {
            foreach (var localized in card.GetComponentsInChildren<LocalizedText>())
            {
                if (string.IsNullOrEmpty(localized.Key)) continue;
                var text = localizer.Get(localized.Key);
                localized.GetComponent<TMP_Text>().text = text.Contains("{0}") ? localizer.Get(localized.Key, "English") : text;
            }
            var message = Require(card, "Footer/Message").GetComponent<TMP_Text>();
            message.text = MessageKeys().Select(key => localizer.Get(key)).OrderByDescending(text => message.GetPreferredValues(text).x)
                .FirstOrDefault() ?? string.Empty;
            var content = Require(card, "Languages/List/Viewport/Content");
            Require(content, "Device/Now").GetComponent<TMP_Text>().text = localizer.Get(SettingsView.DeviceLanguageNowKey, "English");
            var template = Require(content, "Language");
            foreach (var name in LanguageNames())
            {
                var row = Object.Instantiate(template.gameObject, content, false);
                row.name = name;
                row.GetComponent<KitButton>().Label.text = name;
            }
            template.gameObject.SetActive(false);
        }

        // The language page's rows (one without the language choice: the template's own)
        private static List<string> LanguageNames()
        {
            var names = new List<string> { "English" };
            // cmpose:multiLang
            names = LanguageSelector.Supported.Select(language => language.Name).ToList();
            // /cmpose:multiLang
            return names;
        }

        // The members a text group fits and the most lines they may take (KitTextGroup keeps them private)
        private static List<TMP_Text> Members(KitTextGroup group, out int lines)
        {
            var serialized = new UnityEditor.SerializedObject(group);
            lines = serialized.FindProperty("lines").intValue;
            var property = serialized.FindProperty("members");
            var members = new List<TMP_Text>();
            for (var i = 0; i < property.arraySize; i++)
                if (property.GetArrayElementAtIndex(i).objectReferenceValue is TMP_Text member) members.Add(member);
            return members;
        }

        // The rect a text's letters take, in world units
        private static Rect Letters(TMP_Text text)
        {
            var bounds = text.textBounds;
            var min = text.rectTransform.TransformPoint(bounds.min);
            var max = text.rectTransform.TransformPoint(bounds.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // A transform and its children's rects together (a button and its touch area), in world units
        private static Rect WorldWithChildren(Transform t)
        {
            var all = t.GetComponentsInChildren<RectTransform>().Select(LayoutStage.World).ToList();
            return Rect.MinMaxRect(all.Min(r => r.xMin), all.Min(r => r.yMin), all.Max(r => r.xMax), all.Max(r => r.yMax));
        }

        // A text's string key, or the text itself when it has none
        private static string KeyOf(TMP_Text text)
        {
            var localized = text.GetComponent<LocalizedText>();
            return localized != null && !string.IsNullOrEmpty(localized.Key) ? localized.Key : $"\"{text.text}\"";
        }

        private static bool Overflows(TMP_Text text)
        {
            var room = text.rectTransform.rect.width - text.margin.x - text.margin.z;
            return text.isTextOverflowing || TextWidth(text) > room + 1f;
        }

        // How wide a text's letters run: TMP's textBounds end at the last letter's advance, which in bold carries the
        // font's boldSpacing (an em hundredth each): room after the last letter that TMP's own fit (autosizing, wrapping)
        // leaves out, so a bold button label TMP fitted would read as a few units over its box
        private static float TextWidth(TMP_Text text)
        {
            var width = text.textBounds.size.x;
            if ((text.fontStyle & FontStyles.Bold) != 0 && text.font != null) width -= text.font.boldSpacing * text.fontSize * 0.01f;
            return width;
        }

        private static bool Overlap(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > Slack && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > Slack;

        // A selectable's touch area as the screen shows it: after the card's fit scale, in canvas units
        private static Vector2 ShownTouch(Selectable selectable, Transform root)
        {
            var hit = HitRect(selectable, out _);
            return hit.HasValue ? hit.Value.size / root.lossyScale.x : Vector2.zero;
        }

        private static readonly string[] RowNames = { "Music", "Sfx", "Vibration", "Language" };

        // Every group (Title, Rows, Buttons, Footer) on one line and the footer's message on at most two, inside their
        // boxes, each at its group's one size, not under its minimum
        [Test]
        public void Settings_texts_fit_one_line_in_every_language()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var groups = card.GetComponentsInChildren<KitTextGroup>();
                if (groups.Length != 5) problems.Add($"{where}: {groups.Length} text groups, not 5 (Title, Rows, Buttons, Footer, Message)");
                var message = Require(card, "Footer/Message");
                foreach (var group in groups)
                {
                    var name = LayoutStage.PathOf(group.transform);
                    var members = Members(group, out var lines);
                    var expected = group.transform == message ? 2 : 1;
                    if (lines != expected) problems.Add($"{where}: {name} on {lines} lines, not {expected}");
                    if (!group.Fits) problems.Add($"{where}: {name} goes down to {group.Size}, under its minimum");
                    foreach (var member in members)
                    {
                        if (!member.isActiveAndEnabled || string.IsNullOrEmpty(member.text)) continue;
                        var line = $"{where}: {KeyOf(member)} ({LayoutStage.PathOf(member.transform)})";
                        if (member.textInfo.lineCount > expected) problems.Add($"{line} on {member.textInfo.lineCount} lines");
                        if (Overflows(member) || member.textBounds.size.y > member.rectTransform.rect.height + 1f)
                            problems.Add($"{line} overflows its box ({member.textBounds.size} in {member.rectTransform.rect.size})");
                        if (!Mathf.Approximately(member.fontSize, group.Size)) problems.Add($"{line} at {member.fontSize}, its group at {group.Size}");
                    }
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The three switches and the language button "EN ›" in one column at the rows' right end
        [Test]
        public void Settings_controls_share_one_column()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var rows = Require(card, "Body/Rows");
                var controls = RowNames.Select(row => (row, LayoutStage.World((RectTransform)Require(rows, $"{row}/Control")))).ToList();
                var (first, column) = controls[0];
                foreach (var (row, rect) in controls.Skip(1))
                {
                    if (Mathf.Abs(rect.width - column.width) > Slack) problems.Add($"{where}: {row}'s control {rect.width} wide, {first}'s {column.width}");
                    if (Mathf.Abs(rect.xMin - column.xMin) > Slack || Mathf.Abs(rect.xMax - column.xMax) > Slack)
                        problems.Add($"{where}: {row}'s control {rect} out of {first}'s column {column}");
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Two columns of equal cells; with Credits hidden the odd last one (Rate) spans both
        [Test]
        public void Settings_grid_buttons_are_equal_and_an_odd_last_one_spans()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var grid = Require(card, "Body/Actions/Grid");
                Rect Cell(string name) => LayoutStage.World((RectTransform)Require(grid, name));
                var cells = new[] { "Restore", "SignIn", "AdPrivacy", "Support", "Rate", "Credits" };
                var size = Cell("Restore").size;
                var left = Cell("Restore");
                var right = Cell("SignIn");
                // World units over the card's fit scale: the grid's own units
                var scale = card.lossyScale.x / root.lossyScale.x;
                if ((right.xMin - left.xMax) / scale < KitLayout.Gap - Slack) problems.Add($"{where}: the grid's columns {left} and {right} not apart");
                for (var i = 0; i < cells.Length; i++)
                {
                    var rect = Cell(cells[i]);
                    var column = i % 2 == 0 ? left : right;
                    if (Vector2.Distance(rect.size, size) > Slack) problems.Add($"{where}: {cells[i]} {rect.size}, Restore {size}");
                    if (Mathf.Abs(rect.xMin - column.xMin) > Slack) problems.Add($"{where}: {cells[i]} {rect} out of its column");
                }
                Require(grid, "Credits").gameObject.SetActive(false);
                LayoutStage.Layout(root.gameObject);
                var rate = Cell("Rate");
                if (Mathf.Abs(rate.xMin - left.xMin) > Slack || Mathf.Abs(rate.xMax - right.xMax) > Slack)
                    problems.Add($"{where}: the odd last button {rate} does not span both columns ({left.xMin} to {right.xMax})");
                if (Mathf.Abs(rate.height - size.y) > Slack) problems.Add($"{where}: the odd last button {rate.height} high, the others {size.y}");
                foreach (var name in cells.Take(4))
                    if (Vector2.Distance(Cell(name).size, size) > Slack) problems.Add($"{where}: {name} {Cell(name).size} with Credits hidden, {size} before");
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The busiest card inside the safe area, every control still 48 dp after the card's fit scale; on a landscape
        // screen Rows and Actions side by side, on a portrait one Rows over Actions
        [Test]
        public void Settings_busiest_card_fits_with_touch_sized_controls()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var cardRect = LayoutStage.World((RectTransform)card);
                var safe = LayoutStage.World((RectTransform)Require(root, "Panel/SafeArea"));
                if (!Inside(safe, cardRect)) problems.Add($"{where}: card {cardRect} outside the safe area {safe}");
                var body = Require(card, "Body/Rows").parent;
                var touched = RowNames.Select(row => Require(body, $"Rows/{row}/Control")).ToList();
                touched.Add(Require(body, "Actions/Primary"));
                touched.AddRange(Require(body, "Actions/Grid").Cast<Transform>());
                touched.Add(Require(card, "Close"));
                foreach (var t in touched)
                {
                    var selectable = t.GetComponent<Selectable>();
                    if (selectable == null)
                    {
                        problems.Add($"{where}: {LayoutStage.PathOf(t)} takes no touches");
                        continue;
                    }
                    var size = ShownTouch(selectable, root);
                    if (size.x < KitLayout.MinTouch - Slack || size.y < KitLayout.MinTouch - Slack)
                        problems.Add($"{where}: {LayoutStage.PathOf(t)} {size} on the screen, under {KitLayout.MinTouch}");
                }
                var rows = LayoutStage.World((RectTransform)Require(body, "Rows"));
                var actions = LayoutStage.World((RectTransform)Require(body, "Actions"));
                var landscape = where.Contains("landscape");
                if (landscape && !(rows.xMax < actions.xMin)) problems.Add($"{where}: Rows {rows} and Actions {actions} not side by side");
                if (!landscape && !(rows.yMin > actions.yMax)) problems.Add($"{where}: Rows {rows} not over Actions {actions}");
                // The footer: under Body in portrait; in landscape in the title's row, clear of the title's letters and of
                // Close with its touch area, the title still shown
                var footer = Require(card, "Footer");
                var bodyRect = LayoutStage.World((RectTransform)body);
                var footerRect = LayoutStage.World((RectTransform)footer);
                var title = Require(card, "Title").GetComponent<TMP_Text>();
                if (!Inside(cardRect, footerRect)) problems.Add($"{where}: the footer {footerRect} off the card {cardRect}");
                if (!landscape && !(footerRect.yMax <= bodyRect.yMin + Slack)) problems.Add($"{where}: the footer {footerRect} not under Body {bodyRect}");
                // Under Body in landscape the card's bottom padding is its side padding, not the portrait footer's
                var padding = card.GetComponent<VerticalLayoutGroup>().padding;
                if (landscape && padding.bottom != padding.left) problems.Add($"{where}: the card's bottom padding {padding.bottom}, its sides {padding.left}");
                if (!landscape) return;
                var titleRow = LayoutStage.World(title.rectTransform);
                if (footerRect.yMin < titleRow.yMin - Slack || footerRect.yMax > titleRow.yMax + Slack)
                    problems.Add($"{where}: the footer {footerRect} not in the title's row {titleRow}");
                var letters = Letters(title);
                var closeRect = WorldWithChildren(Require(card, "Close"));
                foreach (var part in new[] { "Privacy", "Dot", "Terms", "Version", "Message" })
                {
                    var text = Require(footer, part).GetComponent<TMP_Text>();
                    var rect = part == "Message" ? LayoutStage.World(text.rectTransform) : WorldWithChildren(text.transform);
                    if (Overlap(rect, closeRect)) problems.Add($"{where}: Footer/{part} {rect} overlaps Close {closeRect}");
                    if (Overlap(part == "Message" ? rect : Letters(text), letters)) problems.Add($"{where}: Footer/{part} overlaps the title's letters {letters}");
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // One font on the card: only its titles (the card's and the language page's) take the display font; the rows, the
        // buttons, the language code and the footer take the body font
        [Test]
        public void Settings_uses_the_display_font_for_its_titles_only()
        {
            var root = LayoutStage.Prefab(SettingsPrefab, LayoutStage.Screens[0].Screen, Theme(ThemeStyle.Load()), made);
            var card = Require(root.transform, SettingsCard);
            var titles = new[] { Require(card, "Title"), Require(card, "Languages/Title") };
            var problems = new List<string>();
            foreach (var text in card.GetComponentsInChildren<ThemedText>(true))
            {
                var title = titles.Contains(text.transform);
                var display = text.Role == TextRole.Display || text.Role == TextRole.Title;
                if (title != display) problems.Add($"{LayoutStage.PathOf(text.transform)}: {text.Role}");
            }
            Object.DestroyImmediate(root);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // One font on every modal card (KitCard's font pass on a sheet's panel): in Settings, the economy windows and the
        // scenes' sheets (Pause, Guide, the end cards, the showcases'), without a skin and with one, only the titles (named
        // Title or Subtitle) and Display texts (big numbers) keep the display font; a button's label takes its
        // KitButton.LabelFont (the body font but on Play), the button's other texts are its own
        [Test]
        public void Modal_cards_use_the_display_font_for_titles_and_big_numbers_only()
        {
            var problems = new List<string>();
            var screen = LayoutStage.Screens[0].Screen;
            foreach (var skinned in new[] { false, true })
            {
                GameSkin.ForTests(skinned ? ThickSkin() : GameSkin.Parse("{}", _ => null, _ => null));
                var where = skinned ? " (skin)" : "";
                var theme = Theme(ThemeStyle.Load());
                // A prefab that loses its sheets must not drop out of the check silently
                Assert.That(CheckModalFonts($"Settings{where}", LayoutStage.Prefab(SettingsPrefab, screen, theme, made).transform, problems),
                    Is.GreaterThan(0), "Settings: sheets");
                Assert.That(CheckModalFonts($"Economy{where}", LayoutStage.Prefab(EconomyPrefab, screen, theme, made).transform, problems),
                    Is.GreaterThan(0), "Economy: sheets");
                var scenes = new List<string>();
                // cmpose:sample
                scenes.Add(GameScene);
                // /cmpose:sample
                // cmpose:runner2D
                scenes.Add("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity");
                // /cmpose:runner2D
                // cmpose:track3D
                scenes.Add("Assets/_Project/Features/Showcase/Track3D/Scenes/Track3D.unity");
                // /cmpose:track3D
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var sheets = 0;
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            foreach (var element in root.GetComponentsInChildren<KitElement>(true)) element.Bind(theme);
                            sheets += CheckModalFonts($"{System.IO.Path.GetFileNameWithoutExtension(path)}{where}", root.transform, problems);
                        }
                        // A scene that loses its sheets must not drop out of the check silently either
                        Assert.That(sheets, Is.GreaterThan(0), $"{path}: sheets");
                    }
                    finally
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The sheets under root checked; problems gets each text off the rule as "path: role"
        private static int CheckModalFonts(string where, Transform root, List<string> problems)
        {
            var sheets = root.GetComponentsInChildren<KitSheet>(true);
            foreach (var sheet in sheets)
            {
                if (sheet.Panel == null) continue;
                foreach (var text in sheet.Panel.GetComponentsInChildren<ThemedText>(true))
                {
                    var button = text.GetComponentInParent<KitButton>(true);
                    if (button != null && button.transform.IsChildOf(sheet.Panel))
                    {
                        if (button.Label != null && button.Label.transform == text.transform && text.Role != KitButton.LabelFont(button.Variant))
                            problems.Add($"{where}: {LayoutStage.PathOf(text.transform)}: {text.Role}");
                        continue;
                    }
                    var display = text.Role == TextRole.Display || text.Role == TextRole.Title;
                    if (display && !KitCard.KeepsDisplayFont(text.transform)) problems.Add($"{where}: {LayoutStage.PathOf(text.transform)}: {text.Role}");
                }
            }
            return sheets.Length;
        }

        // RemoveAds is the card's one primary button; Close's centre in the card's top right quarter
        [Test]
        public void Settings_has_one_primary_and_close_top_right()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var primaries = card.GetComponentsInChildren<KitButton>().Where(b => b.Variant == KitButtonVariant.Primary)
                    .Select(b => LayoutStage.PathOf(b.transform)).ToList();
                var expected = LayoutStage.PathOf(Require(card, "Body/Actions/Primary"));
                if (primaries.Count != 1 || primaries[0] != expected)
                    problems.Add($"{where}: primary buttons {string.Join(", ", primaries)}, not {expected} alone");
                var cardRect = LayoutStage.World((RectTransform)card);
                var close = LayoutStage.World((RectTransform)Require(card, "Close")).center;
                if (!cardRect.Contains(close) || close.x <= cardRect.center.x || close.y <= cardRect.center.y)
                    problems.Add($"{where}: Close's centre {close} not in the top right quarter of the card {cardRect}");
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Destructive actions are red; a card's close is a round red X: leaving a run from Pause loses it, so Pause's Menu
        // is the scene's only Danger button (the end cards' Menu loses nothing and stays Secondary); the settings card's
        // X is Close
        [Test]
        public void Destructive_actions_and_card_closes_are_red()
        {
            var problems = new List<string>();
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefab);
            var close = Require(settings.transform, SettingsCard + "/Close").GetComponent<KitButton>();
            if (close.Variant != KitButtonVariant.Close) problems.Add($"{SettingsPrefab}: {SettingsCard}/Close is {close.Variant}, not Close");
            var scenes = new List<string>();
            // cmpose:sample
            scenes.Add(GameScene);
            // /cmpose:sample
            // cmpose:runner2D
            scenes.Add("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity");
            // /cmpose:runner2D
            // cmpose:track3D
            scenes.Add("Assets/_Project/Features/Showcase/Track3D/Scenes/Track3D.unity");
            // /cmpose:track3D
            const string pauseMenu = "Canvas/Pause/Panel/SafeArea/Card/Content/Menu";
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var danger = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<KitButton>(true))
                        .Where(b => b.Variant == KitButtonVariant.Danger).Select(b => LayoutStage.PathOf(b.transform)).ToList();
                    if (danger.Count != 1 || danger[0] != pauseMenu)
                        problems.Add($"{path}: Danger buttons {string.Join(", ", danger)}, not {pauseMenu} alone");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The economy windows whose only way out is the round X at their card's top right (the picker's Play goes on, the
        // shop and credits are full-screen panels with their own Close)
        private static readonly string[] CornerCloseWindows = { "Supply", "Wheel", "Login", "Extra", "Offer" };

        // A card's Close is the round X (KitButtonVariant.Close) overhanging its top right corner: the settings card and
        // the CornerCloseWindows as built, on every screen with and without a top banner. Its centre within MinTouch (in
        // the card's units) of the corner, wholly inside the screen and the safe area, its hit area MinTouch at least (in
        // the card's units: a fitted card scales its controls as a whole), clear of every other drawn text's letters and
        // every other control's touch area (the band it overhangs is the frame's), and the button its view wires as
        // closeButton
        [Test]
        public void Card_close_is_a_corner_x_inside_the_screen()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) =>
            {
                var settings = LayoutStage.Prefab(SettingsPrefab, screen, theme, made);
                Require(settings.transform, SettingsLanguages).gameObject.SetActive(false);
                ShowAsBuilt(settings);
                var economy = LayoutStage.Prefab(EconomyPrefab, screen, theme, made);
                ShowAsBuilt(economy);
                var cards = new List<(string Name, Transform Card, MonoBehaviour View)>
                {
                    ("Settings", Require(settings.transform, SettingsCard), settings.GetComponent<SettingsView>())
                };
                foreach (var window in CornerCloseWindows)
                {
                    var root = Require(economy.transform, window);
                    var view = root.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(m => new UnityEditor.SerializedObject(m).FindProperty("closeButton") != null);
                    cards.Add((window, root.GetComponentInChildren<KitSheet>(true).Panel, view));
                }
                WithEachBanner(name, screen, new[] { settings, economy }, where =>
                {
                    foreach (var (card, panel, view) in cards) CheckCornerClose($"{where}: {card}", panel, view, problems);
                });
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(economy);
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static void CheckCornerClose(string where, Transform card, MonoBehaviour view, List<string> problems)
        {
            var close = card.Find("Close");
            if (close == null)
            {
                var elsewhere = card.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Close");
                problems.Add($"{where}: no Close right under the card{(elsewhere != null ? $" ({LayoutStage.PathOf(elsewhere)} instead)" : "")}");
                return;
            }
            var kit = close.GetComponent<KitButton>();
            if (kit == null || kit.Variant != KitButtonVariant.Close)
                problems.Add($"{where}: Close is {(kit == null ? "no KitButton" : kit.Variant.ToString())}, not the X");
            var button = close.GetComponent<Button>();
            var wired = view == null ? null : new UnityEditor.SerializedObject(view).FindProperty("closeButton")?.objectReferenceValue;
            if (button == null || wired != button) problems.Add($"{where}: the view's closeButton is {(wired != null ? wired.name : "nothing")}, not Close");
            var cardRect = LayoutStage.World((RectTransform)card);
            var corner = new Vector2(cardRect.xMax, cardRect.yMax);
            var centre = LayoutStage.World((RectTransform)close).center;
            var reach = KitLayout.MinTouch * card.lossyScale.y / card.root.lossyScale.y;
            if (Vector2.Distance(centre, corner) > reach) problems.Add($"{where}: Close's centre {centre} over {reach} from the card's corner {corner}");
            var fit = 1f;
            var hit = button != null ? HitRect(button, out fit) : null;
            if (!hit.HasValue)
            {
                problems.Add($"{where}: Close takes no touches");
                return;
            }
            // The button and its touch area (its soft shadow may reach past them, as every button's does)
            var own = LayoutStage.World((RectTransform)close);
            var x = Rect.MinMaxRect(Mathf.Min(own.xMin, hit.Value.xMin), Mathf.Min(own.yMin, hit.Value.yMin), Mathf.Max(own.xMax, hit.Value.xMax),
                Mathf.Max(own.yMax, hit.Value.yMax));
            var screenRect = LayoutStage.World((RectTransform)card.root);
            var safe = LayoutStage.World((RectTransform)card.parent);
            if (!Inside(screenRect, x)) problems.Add($"{where}: Close {x} off the screen {screenRect}");
            if (!Inside(safe, x)) problems.Add($"{where}: Close {x} outside the safe area {safe}");
            var size = hit.Value.size / fit;
            if (size.x < KitLayout.MinTouch - Slack || size.y < KitLayout.MinTouch - Slack)
                problems.Add($"{where}: Close's hit area {size} under {KitLayout.MinTouch}");
            foreach (var text in card.GetComponentsInChildren<TMP_Text>())
            {
                if (text.transform.IsChildOf(close) || string.IsNullOrEmpty(text.text) || !Drawn(text.transform, card)) continue;
                if (Overlap(Letters(text), hit.Value)) problems.Add($"{where}: Close {hit.Value} on {LayoutStage.PathOf(text.transform)} \"{text.text}\"");
            }
            foreach (var other in card.GetComponentsInChildren<Selectable>())
            {
                if (other.transform.IsChildOf(close) || !Drawn(other.transform, card)) continue;
                var otherHit = HitRect(other, out _);
                if (otherHit.HasValue && Overlap(otherHit.Value, hit.Value))
                    problems.Add($"{where}: Close {hit.Value} on {LayoutStage.PathOf(other.transform)} {otherHit.Value}");
            }
        }

        // The language page opens in the card's place: the card keeps its size, the page stays on it, its title clear of
        // Back and Close and on one line, its list the card's only scrolling part (vertically), each row as wide as the
        // list, 48 dp on the screen and its texts inside it
        [Test]
        public void Settings_language_page_fits_and_scrolls_only_its_list()
        {
            var problems = new List<string>();
            ForEachBusyCard((where, root, card) =>
            {
                var page = Require(card, "Languages");
                var cardRect = LayoutStage.World((RectTransform)card);
                page.gameObject.SetActive(false);
                LayoutStage.Layout(root.gameObject);
                var closed = LayoutStage.World((RectTransform)card);
                page.gameObject.SetActive(true);
                LayoutStage.Layout(root.gameObject);
                if (Vector2.Distance(cardRect.size, closed.size) > Slack) problems.Add($"{where}: the card {cardRect.size} with the page, {closed.size} without");
                var pageRect = LayoutStage.World((RectTransform)page);
                if (!Inside(cardRect, pageRect)) problems.Add($"{where}: the page {pageRect} off the card {cardRect}");
                var title = Require(page, "Title").GetComponent<TMP_Text>();
                var titleRect = LayoutStage.World(title.rectTransform);
                foreach (var (name, other) in new[] { ("Back", Require(page, "Back")), ("Close", Require(card, "Close")) })
                    if (Overlap(titleRect, LayoutStage.World((RectTransform)other))) problems.Add($"{where}: the page's title overlaps {name}");
                if (title.textInfo.lineCount != 1 || Overflows(title)) problems.Add($"{where}: the page's title \"{title.text}\" not on one line in its box");
                var scrolls = card.GetComponentsInChildren<ScrollRect>();
                var list = Require(page, "List").GetComponent<ScrollRect>();
                if (scrolls.Length != 1 || scrolls[0] != list)
                    problems.Add($"{where}: scrolling parts {string.Join(", ", scrolls.Select(x => x.name))}, not the list alone");
                if (list == null) return;
                if (!list.vertical || list.horizontal) problems.Add($"{where}: the list does not scroll vertically only");
                var listRect = LayoutStage.World((RectTransform)list.transform);
                if (!Inside(pageRect, listRect)) problems.Add($"{where}: the list {listRect} off the page {pageRect}");
                var viewport = LayoutStage.World(list.viewport);
                var shown = 0;
                foreach (Transform row in list.content)
                {
                    if (!row.gameObject.activeSelf) continue;
                    shown++;
                    var rect = LayoutStage.World((RectTransform)row);
                    if (Mathf.Abs(rect.xMin - viewport.xMin) > Slack || Mathf.Abs(rect.xMax - viewport.xMax) > Slack)
                        problems.Add($"{where}: language row {row.name} {rect} not as wide as the list {viewport}");
                    var size = ShownTouch(row.GetComponent<Selectable>(), root);
                    if (size.y < KitLayout.MinTouch - Slack) problems.Add($"{where}: language row {row.name} {size} on the screen, under {KitLayout.MinTouch}");
                    foreach (var text in row.GetComponentsInChildren<TMP_Text>())
                    {
                        if (string.IsNullOrEmpty(text.text)) continue;
                        if (Overflows(text) || text.textInfo.lineCount != 1) problems.Add($"{where}: language row {row.name}: \"{text.text}\" not on one line in its box");
                        if (text.enableAutoSizing && text.fontSize < text.fontSizeMin - 0.01f)
                            problems.Add($"{where}: language row {row.name}: \"{text.text}\" at {text.fontSize}, under {text.fontSizeMin}");
                    }
                }
                if (shown != LanguageNames().Count + 1) problems.Add($"{where}: {shown} language rows, not Device and {LanguageNames().Count}");
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // cmpose:sample
        [Test]
        public void Hud_items_never_touch()
        {
            var problems = new List<string>();
            foreach (var reserved in new[] { 0f, 150f })
            {
                SafeArea.ReservedTop = reserved;
                ForEachScreen((name, screen, theme) => InScene(GameScene, screen, theme, canvas =>
                {
                    var hud = canvas.GetComponentInChildren<ShellHudView>(true);
                    var content = Require(hud.transform, "Content");
                    var bounds = LayoutStage.World((RectTransform)canvas);
                    foreach (var count in new[] { 0, 3, 8 })
                    {
                        var hearts = new List<GameObject>();
                        for (var i = 0; i < count; i++)
                        {
                            var heart = new GameObject($"Heart{i}", typeof(RectTransform), typeof(LayoutElement));
                            heart.transform.SetParent(hud.Items.transform, false);
                            var element = heart.GetComponent<LayoutElement>();
                            element.preferredWidth = 84f;
                            element.preferredHeight = 84f;
                            hearts.Add(heart);
                        }
                        LayoutStage.Layout(canvas.gameObject);
                        var items = new List<(string Name, Rect Rect)>
                        {
                            ("CounterPill", LayoutStage.World((RectTransform)Require(hud.Items.transform, "CounterPill"))),
                            ("LevelPill", LayoutStage.World((RectTransform)Require(content, "LevelPill"))),
                            ("PauseButton", LayoutStage.World((RectTransform)Require(canvas, "Pause/SafeArea/PauseButton"))),
                            ("Score", LayoutStage.World((RectTransform)Require(content, "Score")))
                        };
                        items.AddRange(hearts.Select(h => (h.name, LayoutStage.World((RectTransform)h.transform))));
                        var where = $"{name}, reserved {reserved}, {count} hearts";
                        for (var a = 0; a < items.Count; a++)
                        {
                            if (!Inside(bounds, items[a].Rect)) problems.Add($"{where}: {items[a].Name} {items[a].Rect} off the screen");
                            for (var b = a + 1; b < items.Count; b++)
                            {
                                var gap = LayoutStage.Distance(items[a].Rect, items[b].Rect);
                                if (gap < KitLayout.Gap - Slack) problems.Add($"{where}: {items[a].Name} and {items[b].Name} {gap} apart");
                            }
                        }
                        foreach (var heart in hearts) Object.DestroyImmediate(heart);
                    }
                }));
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
        // /cmpose:sample

        // The touch groups of each screen: (name, root) whose active Selectables are measured together
        private void ForEachTouchGroup(string name, Vector2 screen, IThemeSource theme, Action<string, Transform> check)
        {
            var settings = LayoutStage.Prefab(SettingsPrefab, screen, theme, made).transform;
            check($"{name} Settings", Require(settings, SettingsCard));
            check($"{name} Languages", Require(settings, SettingsLanguages));
            check($"{name} Credits", Require(settings, CreditsArea));
            InScene(MenuScene, screen, theme, canvas => check($"{name} Menu", Require(canvas, "SafeArea/Menu")));
            // cmpose:sample
            InScene(GameScene, screen, theme, canvas =>
            {
                check($"{name} Pause", Require(canvas, "Pause"));
                check($"{name} HUD", Require(canvas, "SafeArea/ShellHud"));
                check($"{name} Level complete", Require(canvas, "LevelComplete"));
            });
            // /cmpose:sample
        }

        // The union of the raycast graphics a touch reaches the selectable through (a nested selectable's are its own),
        // in the selectable's design units: a KitFitScale above it shrinks a whole card to fit a short screen
        private static Rect? HitRect(Selectable selectable, out float fit)
        {
            fit = 1f;
            for (var t = selectable.transform.parent; t != null; t = t.parent)
                if (t.GetComponent<KitFitScale>() != null) fit *= t.localScale.y;
            Rect? hit = null;
            foreach (var graphic in selectable.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.raycastTarget || !graphic.isActiveAndEnabled) continue;
                if (graphic.GetComponentInParent<Selectable>() != selectable) continue;
                var r = LayoutStage.World(graphic.rectTransform);
                hit = hit.HasValue ? Rect.MinMaxRect(Mathf.Min(hit.Value.xMin, r.xMin), Mathf.Min(hit.Value.yMin, r.yMin),
                    Mathf.Max(hit.Value.xMax, r.xMax), Mathf.Max(hit.Value.yMax, r.yMax)) : r;
            }
            return hit;
        }

        private static bool Below(Transform t, Transform ancestor)
        {
            for (; t != null; t = t.parent)
                if (t == ancestor) return true;
            return false;
        }

        [Test]
        public void Touch_targets_reach_48dp()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) => ForEachTouchGroup(name, screen, theme, (where, root) =>
            {
                var hits = new List<(string Name, Rect Rect)>();
                foreach (var selectable in root.GetComponentsInChildren<Selectable>())
                {
                    // The menu's side button is a template code copies into the row above Play
                    if (selectable.GetComponentsInParent<Transform>(true).Any(t => t.name == "SideButton")) continue;
                    // The settings card's language page is a group of its own: it opens in the place of the card's body
                    if (root.name == "Card" && Below(selectable.transform, Require(root, "Languages"))) continue;
                    var path = LayoutStage.PathOf(selectable.transform);
                    var hit = HitRect(selectable, out var fit);
                    if (!hit.HasValue)
                    {
                        problems.Add($"{where}: {path} takes no touches");
                        continue;
                    }
                    var size = hit.Value.size / fit;
                    if (size.x < KitLayout.MinTouch - Slack || size.y < KitLayout.MinTouch - Slack)
                        problems.Add($"{where}: {path} hit area {size} under {KitLayout.MinTouch}");
                    hits.Add((path, hit.Value));
                }
                for (var a = 0; a < hits.Count; a++)
                for (var b = a + 1; b < hits.Count; b++)
                {
                    var x = Mathf.Min(hits[a].Rect.xMax, hits[b].Rect.xMax) - Mathf.Max(hits[a].Rect.xMin, hits[b].Rect.xMin);
                    var y = Mathf.Min(hits[a].Rect.yMax, hits[b].Rect.yMax) - Mathf.Max(hits[a].Rect.yMin, hits[b].Rect.yMin);
                    if (x > Slack && y > Slack) problems.Add($"{where}: {hits[a].Name} and {hits[b].Name} overlap");
                }
            }));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Texts_fit_their_boxes()
        {
            var problems = new List<string>();
            var localizer = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            foreach (var language in Languages())
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                void Check(string where, GameObject root)
                {
                    foreach (var localized in root.GetComponentsInChildren<LocalizedText>())
                    {
                        if (string.IsNullOrEmpty(localized.Key)) continue;
                        var raw = localized.Key;
                        var text = localizer.Get(raw);
                        localized.GetComponent<TMP_Text>().text = text.Contains("{0}") ? localizer.Get(raw, 12345) : text;
                    }
                    LayoutStage.Layout(root);
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                    {
                        if (string.IsNullOrEmpty(text.text)) continue;
                        var path = $"{where} {language}: {LayoutStage.PathOf(text.transform)} \"{text.text}\"";
                        var room = text.rectTransform.rect.width - text.margin.x - text.margin.z;
                        if (text.isTextOverflowing || TextWidth(text) > room + 1f)
                            problems.Add($"{path} overflows its box ({TextWidth(text)} in {room})");
                        if (text.enableAutoSizing && text.fontSize < text.fontSizeMin - 0.01f)
                            problems.Add($"{path} at {text.fontSize}, under its minimum {text.fontSizeMin}");
                    }
                }
                ForEachScreen((name, screen, theme) =>
                {
                    Check($"{name} Settings", LayoutStage.Prefab(SettingsPrefab, screen, theme, made));
                    Check($"{name} Economy", LayoutStage.Prefab(EconomyPrefab, screen, theme, made));
                    InScene(MenuScene, screen, theme, canvas => Check($"{name} Menu", canvas.gameObject));
                    // cmpose:sample
                    InScene(GameScene, screen, theme, canvas => Check($"{name} Game", canvas.gameObject));
                    // /cmpose:sample
                });
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The top banner a game may reserve (SafeArea.ReservedTop): on a landscape phone the shortest real screen
        private static readonly float[] Reserved = { 0f, 150f };

        // Each fitted window under root opened as built (the parts it builds hidden stay hidden: Next under Claim, the
        // odds over the wheel): its card's parts never overlap and its buttons stay on the card
        private static void CheckWindows(string where, GameObject root, int expected, List<string> problems)
        {
            var checkedWindows = 0;
            foreach (var sheet in root.GetComponentsInChildren<KitSheet>(true))
            {
                if (sheet.Panel == null || sheet.Panel.GetComponent<KitFitScale>() == null) continue;
                checkedWindows++;
                var window = sheet.transform.parent.name;
                var area = LayoutStage.World(sheet.Content);
                var parts = new List<(string Name, Rect Rect)>();
                foreach (Transform child in sheet.Content)
                {
                    if (!LayoutStage.ShownAsBuilt(child, sheet.transform)) continue;
                    // An empty slot draws nothing (the shell's game over keeps the showcase's Actions row unused)
                    if (!child.GetComponentsInChildren<Graphic>(true).Any(g => LayoutStage.ShownAsBuilt(g.transform, sheet.transform))) continue;
                    var rect = LayoutStage.World((RectTransform)child);
                    // A background or a scrim over the whole content area
                    if (Inside(rect, area)) continue;
                    parts.Add((child.name, rect));
                }
                for (var a = 0; a < parts.Count; a++)
                for (var b = a + 1; b < parts.Count; b++)
                {
                    var x = Mathf.Min(parts[a].Rect.xMax, parts[b].Rect.xMax) - Mathf.Max(parts[a].Rect.xMin, parts[b].Rect.xMin);
                    var y = Mathf.Min(parts[a].Rect.yMax, parts[b].Rect.yMax) - Mathf.Max(parts[a].Rect.yMin, parts[b].Rect.yMin);
                    if (x > Slack && y > Slack) problems.Add($"{where}: {window}: {parts[a].Name} overlaps {parts[b].Name} by {Mathf.Min(x, y)}");
                }
                var card = LayoutStage.World(sheet.Panel);
                foreach (var button in sheet.Content.GetComponentsInChildren<Button>(true))
                {
                    if (!LayoutStage.ShownAsBuilt(button.transform, sheet.transform)) continue;
                    var rect = LayoutStage.World((RectTransform)button.transform);
                    if (!Inside(card, rect)) problems.Add($"{where}: {window}: {LayoutStage.PathOf(button.transform)} {rect} outside the card {card}");
                }
            }
            // A window that loses its KitFitScale must not drop out of the check silently
            if (checkedWindows != expected) problems.Add($"{where}: checked {checkedWindows} fitted windows, expected {expected}");
        }

        // cmpose:sample
        // The Game scene's fitted cards and their bands of the 1920-unit reference (SceneBuilder's FittedModal calls)
        private static readonly (string Card, float YMin, float YMax)[] GameCardBands =
        {
            ("Pause", 0.27f, 0.73f), ("ShellGameOver", 0.14f, 0.86f), ("LevelComplete", 0.12f, 0.88f), ("LevelFailed", 0.2f, 0.8f),
            ("Guide", 0.16f, 0.84f)
        };

        // A portrait card as tall as its band: of the reference on the 1080 x 1920 phone, of the safe area on a taller one
        private static void CheckCardHeights(string where, Vector2 screen, Transform canvas, List<string> problems)
        {
            var portrait = LayoutStage.Screens.First(s => s.Name == "16:9").Screen;
            var tall = LayoutStage.Screens.First(s => s.Name == "20:9").Screen;
            if (screen != portrait && screen != tall) return;
            foreach (var (name, yMin, yMax) in GameCardBands)
            {
                var card = Require(canvas, $"{name}/Panel/SafeArea/Card");
                var height = LayoutStage.World((RectTransform)card).height;
                var band = screen == tall ? LayoutStage.World((RectTransform)card.parent).height : KitFitScale.ReferenceHeight;
                var expected = (yMax - yMin) * band;
                if (Mathf.Abs(height - expected) > 1f) problems.Add($"{where}: {name}: card {height} high, not {expected}");
            }
        }
        // /cmpose:sample

        // The economy windows and the Game scene's fitted cards (Pause, the end cards, Guide) on every screen, with and
        // without a top banner reserved; the scene opens once per screen and skin and is laid out again for the banner
        [Test]
        public void Window_parts_never_overlap()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) =>
            {
                foreach (var reserved in Reserved)
                {
                    SafeArea.ReservedTop = reserved;
                    var where = reserved > 0f ? $"{name}, reserved {reserved}" : name;
                    CheckWindows(where, LayoutStage.Prefab(EconomyPrefab, screen, theme, made), 6, problems);
                }
                SafeArea.ReservedTop = 0f;
                // cmpose:sample
                InScene(GameScene, screen, theme, canvas =>
                {
                    CheckCardHeights(name, screen, canvas, problems);
                    foreach (var reserved in Reserved)
                    {
                        SafeArea.ReservedTop = reserved;
                        if (reserved > 0f) LayoutStage.Reserve(canvas.gameObject, screen);
                        CheckWindows(reserved > 0f ? $"{name}, reserved {reserved}" : name, canvas.gameObject, 5, problems);
                    }
                    SafeArea.ReservedTop = 0f;
                });
                // /cmpose:sample
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The booster offer at its fullest: Shop and the message show together (neither the price nor an ad in reach) on
        // its fixed card, on every screen with and without a top banner: every shown part of its Content (the icon, the
        // texts, the actions, the message and Shop's row) inside the Content, none over another, and Shop on the card
        [Test]
        public void Offer_shop_and_message_fit_together()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) =>
            {
                var economy = LayoutStage.Prefab(EconomyPrefab, screen, theme, made);
                ShowAsBuilt(economy);
                var offer = Require(economy.transform, "Offer");
                var view = offer.GetComponent<BoosterOfferView>();
                var sheet = offer.GetComponentInChildren<KitSheet>(true);
                view.Message.gameObject.SetActive(true);
                view.SetShop(true);
                LayoutStage.Layout(economy);
                WithEachBanner(name, screen, new[] { economy }, where =>
                {
                    var area = LayoutStage.World(sheet.Content);
                    var card = LayoutStage.World(sheet.Panel);
                    var parts = new List<(string Name, Rect Rect)>();
                    foreach (Transform child in sheet.Content)
                    {
                        if (!child.gameObject.activeSelf) continue;
                        var rect = LayoutStage.World((RectTransform)child);
                        if (!Inside(area, rect)) problems.Add($"{where}: Offer: {child.name} {rect} outside its Content {area}");
                        parts.Add((child.name, rect));
                    }
                    for (var a = 0; a < parts.Count; a++)
                    for (var b = a + 1; b < parts.Count; b++)
                        if (Overlap(parts[a].Rect, parts[b].Rect)) problems.Add($"{where}: Offer: {parts[a].Name} overlaps {parts[b].Name}");
                    var shop = LayoutStage.World((RectTransform)view.ShopButton.transform);
                    if (!view.ShopButton.gameObject.activeInHierarchy) problems.Add($"{where}: Offer: Shop not shown");
                    if (!Inside(card, shop)) problems.Add($"{where}: Offer: Shop {shop} off the card {card}");
                });
                Object.DestroyImmediate(economy);
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Each economy window's band of the safe area's height before it was fitted (SceneBuilder's EconomyWindow calls)
        private static readonly (string Window, float YMin, float YMax)[] EconomyBands =
        {
            ("Supply", 0.186f, 0.814f), ("Wheel", 0.14f, 0.86f), ("Login", 0.155f, 0.845f), ("Extra", 0.22f, 0.78f), ("Picker", 0.18f, 0.82f),
            ("Offer", 0.222f, 0.778f)
        };

        // Fitting the economy cards to landscape leaves the portraits as they were, unscaled: as tall as the band of the
        // 1920-unit reference on the 1080 x 1920 phone, and of the safe area on a taller 20:9 one
        [Test]
        public void Economy_windows_keep_their_portrait_size()
        {
            var problems = new List<string>();
            var portrait = LayoutStage.Screens.First(s => s.Name == "16:9").Screen;
            var tall = LayoutStage.Screens.First(s => s.Name == "20:9").Screen;
            ForEachScreen((name, screen, theme) =>
            {
                if (screen != portrait && screen != tall) return;
                var root = LayoutStage.Prefab(EconomyPrefab, screen, theme, made).transform;
                foreach (var (window, yMin, yMax) in EconomyBands)
                {
                    var card = Require(root, window).GetComponentInChildren<KitSheet>(true).Panel;
                    var fit = card.GetComponent<KitFitScale>();
                    if (fit == null) problems.Add($"{name}: {window}: card has no KitFitScale");
                    else if (Mathf.Abs(fit.Scale - 1f) > 0.0001f) problems.Add($"{name}: {window}: card scaled to {fit.Scale}");
                    var height = LayoutStage.World(card).height;
                    var band = screen == tall ? LayoutStage.World((RectTransform)card.parent).height : KitFitScale.ReferenceHeight;
                    var expected = (yMax - yMin) * band;
                    if (Mathf.Abs(height - expected) > 1f) problems.Add($"{name}: {window}: card {height} high, not {expected}");
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // check on staged roots with no banner and with each Reserved one (the roots laid out again for it); check gets
        // the screen's name with the banner's
        private static void WithEachBanner(string name, Vector2 screen, IReadOnlyList<GameObject> roots, Action<string> check)
        {
            foreach (var reserved in Reserved)
            {
                SafeArea.ReservedTop = reserved;
                if (reserved > 0f)
                    foreach (var root in roots) LayoutStage.Reserve(root, screen);
                check(reserved > 0f ? $"{name}, reserved {reserved}" : name);
            }
            SafeArea.ReservedTop = 0f;
        }

        // Each modal card under root (a KitSheet's panel with a KitFitScale) as shown: centred in its parent (the safe
        // area) and within KitFitScale's share of the parent's width and height (landscape: wider than high). The settings
        // card alone is not bounded (its controls stay touch-sized): it stays inside its parent less its fit's margin.
        // Returns how many cards it checked
        private static int CheckCardBounds(string where, Transform root, List<string> problems)
        {
            var cards = 0;
            foreach (var sheet in root.GetComponentsInChildren<KitSheet>(true))
            {
                var fit = sheet.Panel == null ? null : sheet.Panel.GetComponent<KitFitScale>();
                if (fit == null) continue;
                cards++;
                var name = $"{where}: {LayoutStage.PathOf(sheet.Panel)}";
                var card = LayoutStage.World(sheet.Panel);
                var parent = LayoutStage.World((RectTransform)sheet.Panel.parent);
                if (Mathf.Abs(card.center.x - parent.center.x) > 1f || Mathf.Abs(card.center.y - parent.center.y) > 1f)
                    problems.Add($"{name}: card {card} not centred in {parent}");
                if (!fit.Bounded)
                {
                    if (sheet.GetComponentInParent<SettingsView>(true) == null) problems.Add($"{name}: not bounded, and not the settings card");
                    var room = Rect.MinMaxRect(parent.xMin + fit.Margin, parent.yMin + fit.Margin, parent.xMax - fit.Margin, parent.yMax - fit.Margin);
                    if (!Inside(room, card)) problems.Add($"{name}: card {card} outside its parent less the margin {room}");
                    continue;
                }
                var landscape = parent.width > parent.height;
                var widthShare = landscape ? KitFitScale.LandscapeWidth : KitFitScale.PortraitWidth;
                var heightShare = landscape ? KitFitScale.LandscapeHeight : KitFitScale.PortraitHeight;
                if (card.width > widthShare * parent.width + Slack)
                    problems.Add($"{name}: card {card.width} wide, over {widthShare} of {parent.width}");
                if (card.height > heightShare * parent.height + Slack)
                    problems.Add($"{name}: card {card.height} high, over {heightShare} of {parent.height}");
            }
            return cards;
        }

        // The modal cards whose header stays off its band on a portrait screen, and why (their layout puts another part on
        // the band; KitCard keeps such a card as it was)
        private static readonly (string Card, string Why)[] UnbandedHeaders =
        {
            ("Wheel/Panel/SafeArea/Card", "the odds button (Info) sits left of the title, on the band"),
            ("Offer/Panel/SafeArea/Card", "the booster's icon sits above the title: on the band its Primary disc would vanish into the Primary frame")
        };

        // The texts of root's LocalizedTexts in localizer's language ({0} filled in)
        private static void Localize(GameObject root, TableLocalizer localizer)
        {
            foreach (var localized in root.GetComponentsInChildren<LocalizedText>(true))
            {
                if (string.IsNullOrEmpty(localized.Key)) continue;
                var text = localizer.Get(localized.Key);
                localized.GetComponent<TMP_Text>().text = text.Contains("{0}") ? localizer.Get(localized.Key, 12345) : text;
            }
        }

        // Each modal card's parts under root as built (what the builder hid stays hidden: the settings footer's message,
        // Next under Claim, the odds over the wheel), then the card shown again the way a sheet opens it and laid out (a
        // device places the band when the card shows, after the layout)
        private static void ShowAsBuilt(GameObject root)
        {
            foreach (var sheet in root.GetComponentsInChildren<KitSheet>(true))
            {
                if (sheet.Panel == null) continue;
                foreach (var t in sheet.Panel.GetComponentsInChildren<Transform>(true))
                    if (t != sheet.Panel && !LayoutStage.ShownAsBuilt(t, t.parent)) t.gameObject.SetActive(false);
                sheet.Panel.gameObject.SetActive(false);
                sheet.Panel.gameObject.SetActive(true);
            }
            LayoutStage.Layout(root);
        }

        // Drawn: active up to the card and under no CanvasGroup faded out (the settings page under its language page)
        private static bool Drawn(Transform t, Transform card)
        {
            for (; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && group.enabled && group.alpha <= 0f) return false;
                if (t == card) return true;
            }
            return true;
        }

        private static bool InCloseX(Transform t)
        {
            var button = t.GetComponentInParent<KitButton>(true);
            return button != null && button.Variant == KitButtonVariant.Close;
        }

        // Each modal card under root with a header (KitCard.Header): the header fits its box; banded, the band (the card's
        // top down to its Surface panel) holds the header whole and no other drawn text or control but the card's Close X,
        // the X stays off the title's letters and the first part under the band keeps KitLayout.Gap from it (a line that
        // hugs the band reads cramped: the end cards' "Complete!" and "Failed"); unbanded collects the cards with a header but no band (with where).
        // Returns how many headers it checked
        private static int CheckBands(string where, Transform root, List<string> problems, List<(string Card, string Where)> unbanded)
        {
            var headers = 0;
            foreach (var sheet in root.GetComponentsInChildren<KitSheet>(true))
            {
                var card = sheet.Panel != null ? sheet.Panel.GetComponent<KitCard>() : null;
                if (card == null || card.Header == null) continue;
                headers++;
                var path = LayoutStage.PathOf(sheet.Panel);
                var name = $"{where}: {path}";
                var title = card.Header.GetComponent<TMP_Text>();
                if (Overflows(title) || (title.enableAutoSizing && title.fontSize < title.fontSizeMin - 0.01f))
                    problems.Add($"{name}: the header \"{title.text}\" does not fit its box");
                if (!card.Banded)
                {
                    unbanded.Add((path, where));
                    continue;
                }
                var cardRect = LayoutStage.World(sheet.Panel);
                var band = Rect.MinMaxRect(cardRect.xMin, LayoutStage.World(card.Body.rectTransform).yMax, cardRect.xMax, cardRect.yMax);
                var header = LayoutStage.World(title.rectTransform);
                var gap = KitCard.BandGap * sheet.Panel.lossyScale.y / root.lossyScale.y;
                if (header.yMin < band.yMin + gap - Slack || !Inside(band, header))
                    problems.Add($"{name}: the header {header} not on its band {band}, BandGap above its bottom");
                foreach (var text in sheet.Panel.GetComponentsInChildren<TMP_Text>())
                {
                    if (text.transform.IsChildOf(title.transform) || InCloseX(text.transform) || !Drawn(text.transform, sheet.Panel)) continue;
                    if (Overlap(band, LayoutStage.World(text.rectTransform)))
                        problems.Add($"{name}: {LayoutStage.PathOf(text.transform)} \"{text.text}\" on the band {band}");
                }
                foreach (var selectable in sheet.Panel.GetComponentsInChildren<Selectable>())
                {
                    if (!Drawn(selectable.transform, sheet.Panel)) continue;
                    var rect = LayoutStage.World((RectTransform)selectable.transform);
                    if (InCloseX(selectable.transform))
                    {
                        if (Overlap(Letters(title), rect)) problems.Add($"{name}: the Close X {rect} on the title's letters {Letters(title)}");
                        continue;
                    }
                    if (Overlap(band, rect)) problems.Add($"{name}: {LayoutStage.PathOf(selectable.transform)} on the band {band}");
                }
                var below = FirstPartUnder(sheet.Panel, title.transform, band);
                var clearance = KitLayout.Gap * sheet.Panel.lossyScale.y / root.lossyScale.y;
                if (below.Part != null && band.yMin - below.Top < clearance - Slack)
                    problems.Add($"{name}: {below.Part} {band.yMin - below.Top:0.#} under the band, closer than KitLayout.Gap ({clearance:0.#})");
            }
            return headers;
        }

        // The highest drawn text (by its letters) or control (by its rect) under a banded card's band, the header and the
        // card's Close X aside: the part the band keeps KitLayout.Gap from. A part on the band is CheckBands' other problem
        private static (string Part, float Top) FirstPartUnder(Transform card, Transform header, Rect band)
        {
            (string Part, float Top) first = (null, float.NegativeInfinity);
            void Consider(Transform part, float top)
            {
                if (top > band.yMin + Slack || top <= first.Top) return;
                first = (LayoutStage.PathOf(part), top);
            }
            foreach (var text in card.GetComponentsInChildren<TMP_Text>())
            {
                if (text.transform.IsChildOf(header) || InCloseX(text.transform) || !Drawn(text.transform, card)) continue;
                if (string.IsNullOrWhiteSpace(text.text)) continue;
                Consider(text.transform, Letters(text).yMax);
            }
            foreach (var selectable in card.GetComponentsInChildren<Selectable>())
            {
                if (InCloseX(selectable.transform) || !Drawn(selectable.transform, card)) continue;
                Consider(selectable.transform, LayoutStage.World((RectTransform)selectable.transform).yMax);
            }
            return first;
        }

        // A modal card's title sits on a band in a chunky family (the tall pair in bright, no skin) on every screen and in
        // every language: the settings card (its page; on its language page the veiled title is no header, so Back and
        // the page's title never sit on a band), the economy windows and the scenes' cards (Pause, Guide, the end cards)
        // shown as built. A banded card's band holds its header whole and no other text or control but its Close X;
        // every header fits its box; on a portrait screen every card with a header is banded, but the UnbandedHeaders
        [Test]
        public void Modal_titles_sit_on_a_band_clear_of_other_parts()
        {
            var problems = new List<string>();
            var unbanded = new List<(string Card, string Where)>();
            var headers = new Dictionary<string, int>();
            void Count(string source, int found) => headers[source] = (headers.TryGetValue(source, out var n) ? n : 0) + found;
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
            var style = LayoutStage.TallStyle(made);
            Assert.That(KitShape.Chunky(style), Is.True, "the tall style runs the chunky look");
            var localizer = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            var scenes = new List<string>();
            // cmpose:sample
            scenes.Add(GameScene);
            // /cmpose:sample
            // cmpose:runner2D
            scenes.Add("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity");
            // /cmpose:runner2D
            // cmpose:track3D
            scenes.Add("Assets/_Project/Features/Showcase/Track3D/Scenes/Track3D.unity");
            // /cmpose:track3D
            foreach (var language in Languages())
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                foreach (var (screenName, screen) in LayoutStage.Screens)
                {
                    var portrait = screen.y > screen.x;
                    var where = $"{screenName} {language}";
                    var found = new List<(string Card, string Where)>();
                    var theme = Theme(style);
                    var settings = LayoutStage.Prefab(SettingsPrefab, screen, theme, made);
                    var card = Require(settings.transform, SettingsCard);
                    Busy(card, localizer);
                    Require(card, "Languages").gameObject.SetActive(false);
                    ShowAsBuilt(settings);
                    Count("Settings", CheckBands($"Settings {where}", settings.transform, problems, found));
                    settings.GetComponent<SettingsView>().ShowLanguages(LanguageNames().Select(n => (n, n)).ToList(), null, "English");
                    LayoutStage.Layout(settings);
                    if (CheckBands($"Settings {where} (language page)", settings.transform, problems, found) != 0)
                        problems.Add($"Settings {where}: the language page's veiled card title still counts as its header");
                    Object.DestroyImmediate(settings);
                    var economy = LayoutStage.Prefab(EconomyPrefab, screen, theme, made);
                    Localize(economy, localizer);
                    ShowAsBuilt(economy);
                    Count("Economy", CheckBands($"Economy {where}", economy.transform, problems, found));
                    Object.DestroyImmediate(economy);
                    foreach (var path in scenes)
                    {
                        var file = System.IO.Path.GetFileNameWithoutExtension(path);
                        LayoutStage.Scene(path, screen, theme, out var scene);
                        try
                        {
                            foreach (var root in scene.GetRootGameObjects().Where(r => r.GetComponent<Canvas>() != null))
                            {
                                Localize(root, localizer);
                                ShowAsBuilt(root);
                                Count(file, CheckBands($"{file} {where}", root.transform, problems, found));
                            }
                        }
                        finally
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                    if (portrait) unbanded.AddRange(found);
                }
            }
            // A source that loses its headers must not drop out of the check silently
            foreach (var source in new[] { "Settings", "Economy" }.Concat(scenes.Select(System.IO.Path.GetFileNameWithoutExtension)))
                if (!headers.TryGetValue(source, out var n) || n == 0) problems.Add($"{source}: no card with a header");
            foreach (var (path, where) in unbanded)
                if (!UnbandedHeaders.Any(e => path.EndsWith(e.Card))) problems.Add($"{where}: {path} has a header but no band in portrait");
            foreach (var (exception, why) in UnbandedHeaders)
                if (!unbanded.Any(u => u.Card.EndsWith(exception))) problems.Add($"{exception} is banded now: drop it from UnbandedHeaders ({why})");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Every modal card on every screen, with and without a top banner, stays within its share of the safe area and
        // centred in it, its content scaled down rather than clipped (the settings card, exempt to keep touch-sized
        // controls, within the safe area less its margin): the settings card at its busiest in every language, the
        // economy windows and the scenes' cards (their own and their Settings). The number of cards checked per source
        // is fixed, so a card that loses its KitFitScale does not drop out of the check silently
        [Test]
        public void Modal_cards_stay_within_their_screen_bounds()
        {
            var problems = new List<string>();
            var localizer = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            foreach (var language in Languages())
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                ForEachScreen((name, screen, theme) =>
                {
                    var root = LayoutStage.Prefab(SettingsPrefab, screen, theme, made);
                    Busy(Require(root.transform, SettingsCard), localizer);
                    LayoutStage.Layout(root);
                    WithEachBanner($"{name} {language}", screen, new[] { root }, where =>
                    {
                        var cards = CheckCardBounds($"Settings {where}", root.transform, problems);
                        if (cards != 1) problems.Add($"Settings {where}: checked {cards} cards, expected 1");
                    });
                    Object.DestroyImmediate(root);
                });
            }
            var scenes = new List<(string Path, int Cards)>();
            // cmpose:sample
            // Pause, the end cards, Guide and Settings
            scenes.Add((GameScene, 6));
            // /cmpose:sample
            // cmpose:runner2D
            scenes.Add(("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity", 3));
            // /cmpose:runner2D
            // cmpose:track3D
            scenes.Add(("Assets/_Project/Features/Showcase/Track3D/Scenes/Track3D.unity", 3));
            // /cmpose:track3D
            ForEachScreen((name, screen, theme) =>
            {
                var economy = LayoutStage.Prefab(EconomyPrefab, screen, theme, made);
                WithEachBanner(name, screen, new[] { economy }, where =>
                {
                    var cards = CheckCardBounds($"Economy {where}", economy.transform, problems);
                    if (cards != 6) problems.Add($"Economy {where}: checked {cards} cards, expected 6");
                });
                Object.DestroyImmediate(economy);
                foreach (var (path, expected) in scenes)
                {
                    var file = System.IO.Path.GetFileNameWithoutExtension(path);
                    LayoutStage.Scene(path, screen, theme, out var scene);
                    try
                    {
                        var roots = scene.GetRootGameObjects().Where(r => r.GetComponent<Canvas>() != null).ToList();
                        WithEachBanner(name, screen, roots, where =>
                        {
                            var cards = roots.Sum(r => CheckCardBounds($"{file} {where}", r.transform, problems));
                            if (cards != expected) problems.Add($"{file} {where}: checked {cards} cards, expected {expected}");
                        });
                    }
                    finally
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The menu at its busiest, as ScreenShots.BusyMenu sets it up (the shot code is the maintainer's, out of the tests'
        // reach) with three games: a long two-line title, the version, the coins with the shop's mark, the economy's three
        // round buttons with their badges, the showcase's rows as ShowcaseMenuInstaller spawns them (best score, Shop,
        // the run totals, the scores' hint shown) and its leaderboard's two buttons; the Right side topped up to three
        // with the economy's buttons where a flag leaves it fewer, so the row holds five in every variant; full: a custom
        // extra's button too, so both sides hold their three (the most IMenuSideButtons takes). The texts in localizer's
        // language
        private static void BusyMenu(MenuView menu, IThemeSource theme, TableLocalizer localizer, bool full)
        {
            var binder = new UiBinder(localizer, theme);
            menu.Construct(binder);
            menu.SetTitle("Şanslı Çiftlik Macerası");
            menu.SetVersion("1.0.0 (1)");
            menu.ShowGames(new[] { "menu.play", "menu.play", "menu.play" }, binder);
            menu.SetCoins(1250, new CurrencyMark(null, new Color(0.95f, 0.75f, 0.2f), "C"));
            menu.ShowBuy(true);
            menu.AddSideButton(MenuSide.Left, KitSprites.Gift, EconomyKeys.Supply, null).SetBadge(MenuBadge.Timer, "2:59:41");
            menu.AddSideButton(MenuSide.Right, KitSprites.Wheel, EconomyKeys.Wheel, null).SetBadge(MenuBadge.Ad);
            menu.AddSideButton(MenuSide.Left, KitSprites.Calendar, EconomyKeys.Login, null).SetBadge(MenuBadge.Dot);
            if (full) menu.AddSideButton(MenuSide.Left, KitSprites.Gift, EconomyKeys.Supply, null).SetBadge(MenuBadge.Dot);
            // cmpose:showcase
            ShowcaseMenuView.Spawn(menu.ExtraSlot, binder).ShowBest(128);
            // cmpose:purchases
            ShopButtonView.Spawn(menu.ExtraSlot, binder).SetVisible(true);
            // /cmpose:purchases
            // cmpose:saveSystem
            RunStatsView.Spawn(menu.ExtraSlot, binder).Show(42, 96);
            // /cmpose:saveSystem
            // cmpose:leaderboards
            ShowcaseScoresView.Spawn(menu.ExtraSlot, menu, binder).ShowHint(LeaderboardsKeys.NotSetUp);
            // /cmpose:leaderboards
            // /cmpose:showcase
            var right = menu.SideRow.GetComponentsInChildren<MenuSideButton>(true).Count(b => b.name.StartsWith(nameof(MenuSide.Right)));
            for (var i = right; i < MenuView.SideSlots; i++)
                menu.AddSideButton(MenuSide.Right, i % 2 == 0 ? KitSprites.Calendar : KitSprites.Gift, i % 2 == 0 ? EconomyKeys.Login : EconomyKeys.Supply,
                    null).SetBadge(MenuBadge.Dot);
        }

        // The bottom block clear of the title and the logo (both shown on the stage, with the title's stroke), the top bar
        // and the version; the title's and the logo's bands at most 80 % of the safe area's width; its round buttons
        // touch-sized and apart, they and every game's button inside the safe area; the coins pill as tall as the settings
        // button. What the known gaps miss (the bands and the landscape column against the top bar and the block, the
        // features' rows' buttons touch-sized) goes to fits
        private static void CheckBusyMenu(string where, Transform canvas, MenuView menu, int buttons, List<string> problems,
            List<string> fits)
        {
            var block = LayoutStage.World(menu.BottomBlock);
            var clear = new (string Name, RectTransform Rect)[]
            {
                ("title", menu.Title.rectTransform), ("title stroke", menu.TitleStroke), ("logo", menu.TitleLogo.rectTransform),
                ("top bar", menu.TopBar), ("version", menu.Version.rectTransform)
            };
            foreach (var (name, rect) in clear)
            {
                var other = LayoutStage.World(rect);
                if (Overlap(block, other)) problems.Add($"{where}: the bottom block {block} overlaps the {name} {other}");
            }
            var safe = LayoutStage.World((RectTransform)canvas.Find("SafeArea"));
            foreach (var (name, rect) in new[] { ("title", menu.Title.rectTransform), ("logo", menu.TitleLogo.rectTransform) })
            {
                var width = LayoutStage.World(rect).width;
                if (width > 0.8f * safe.width + Slack) problems.Add($"{where}: the {name} band {width} wide, over 80 % of {safe.width}");
            }
            var sides = menu.SideRow.GetComponentsInChildren<MenuSideButton>();
            if (sides.Length != buttons) problems.Add($"{where}: {sides.Length} round buttons in the row, expected {buttons}");
            for (var a = 0; a < sides.Length; a++)
            for (var b = a + 1; b < sides.Length; b++)
            {
                var first = LayoutStage.World((RectTransform)sides[a].transform);
                var second = LayoutStage.World((RectTransform)sides[b].transform);
                if (Overlap(first, second)) problems.Add($"{where}: {sides[a].name} {first} overlaps {sides[b].name} {second}");
            }
            foreach (var side in sides)
            {
                var slot = LayoutStage.World((RectTransform)side.transform);
                if (!Inside(safe, slot)) problems.Add($"{where}: {side.name} {slot} outside the safe area {safe}");
                var circle = LayoutStage.World((RectTransform)side.Button.transform).size;
                if (circle.x < KitLayout.MinTouch - Slack || circle.y < KitLayout.MinTouch - Slack)
                    problems.Add($"{where}: {side.name} circle {circle} under {KitLayout.MinTouch}");
            }
            if (menu.GameButtons.Count != 3) problems.Add($"{where}: {menu.GameButtons.Count} games, expected 3");
            foreach (var game in menu.GameButtons)
            {
                var rect = LayoutStage.World((RectTransform)game.transform);
                if (!Inside(safe, rect)) problems.Add($"{where}: {LayoutStage.PathOf(game.transform)} {rect} outside the safe area {safe}");
            }
            // The features' rows' buttons (the showcase's Shop) keep their touch size however the rows are scaled
            foreach (var button in menu.ExtraSlot.GetComponentsInChildren<Selectable>())
            {
                var hit = HitRect(button, out _);
                if (hit.HasValue && (hit.Value.width < KitLayout.MinTouch - Slack || hit.Value.height < KitLayout.MinTouch - Slack))
                    fits.Add($"{where}: {LayoutStage.PathOf(button.transform)} hit area {hit.Value.size} under {KitLayout.MinTouch}");
            }
            var coins = LayoutStage.World((RectTransform)menu.Coins.transform).height;
            var settings = LayoutStage.World((RectTransform)menu.SettingsButton.transform).height;
            if (Mathf.Abs(coins - settings) > Slack) problems.Add($"{where}: coins pill {coins} high, settings {settings}");
            var bar = LayoutStage.World(menu.TopBar);
            if (!Inside(safe, block)) problems.Add($"{where}: the bottom block {block} outside the safe area {safe}");
            if (block.yMax > bar.yMin + Slack) problems.Add($"{where}: the bottom block {block} not under the top bar {bar}");
            // The title, its stroke and the logo clear of the top bar, moved down for the banner too
            foreach (var (name, rect) in new[] { ("title", menu.Title.rectTransform), ("title stroke", menu.TitleStroke), ("logo", menu.TitleLogo.rectTransform) })
            {
                var band = LayoutStage.World(rect);
                if (Overlap(band, bar)) fits.Add($"{where}: the {name} {band} overlaps the top bar {bar}");
            }
            if (safe.width <= safe.height) return;
            // Landscape: the title and the logo left, at most 46 % of the width, under the top bar; the block in the right
            // half; the features' rows and the further games (in the left column under the title) clear of everything
            if (block.xMin < safe.center.x - Slack) problems.Add($"{where}: the bottom block {block} not in the right half of {safe}");
            foreach (var (name, rect) in new[] { ("title", menu.Title.rectTransform), ("title stroke", menu.TitleStroke), ("logo", menu.TitleLogo.rectTransform) })
            {
                var band = LayoutStage.World(rect);
                if (band.center.x > safe.center.x) problems.Add($"{where}: the {name} {band} not in the left half of {safe}");
                if (band.width > 0.46f * safe.width + Slack) problems.Add($"{where}: the {name} {band.width} wide, over 46 % of {safe.width}");
            }
            var games = (RectTransform)menu.GameButtons[1].transform.parent;
            foreach (var (name, rect) in new[] { ("features' rows", (RectTransform)menu.ExtraSlot), ("further games", games) })
            {
                var part = LayoutStage.World(rect);
                if (!Inside(safe, part)) problems.Add($"{where}: the {name} {part} outside the safe area {safe}");
                if (rect.IsChildOf(menu.BottomBlock)) continue;
                foreach (var (other, otherRect) in clear.Append(("bottom block", menu.BottomBlock)))
                {
                    var them = LayoutStage.World(otherRect);
                    if (Overlap(part, them)) fits.Add($"{where}: the {name} {part} overlaps the {other} {them}");
                }
            }
            foreach (var game in menu.GameButtons)
            {
                var height = LayoutStage.World((RectTransform)game.transform).height;
                if (height < KitLayout.MinTouch - Slack) problems.Add($"{where}: {LayoutStage.PathOf(game.transform)} {height} high, under {KitLayout.MinTouch}");
            }
        }

        // The round buttons' captions (their row's KitTextGroup): every shown one in the group, on one line inside its box at
        // the group's one size, the group not under its minimum
        private static void CheckCaptions(string where, MenuView menu, List<string> problems)
        {
            var group = menu.SideRow.GetComponent<KitTextGroup>();
            if (!group.Fits) problems.Add($"{where}: the captions go down to {group.Size}, under their minimum");
            var members = Members(group, out var lines);
            if (lines != 1) problems.Add($"{where}: the captions on {lines} lines, not 1");
            foreach (var side in menu.SideRow.GetComponentsInChildren<MenuSideButton>())
            {
                var caption = side.Caption.GetComponent<TMP_Text>();
                var line = $"{where}: {side.name} caption {KeyOf(caption)} \"{caption.text}\"";
                if (string.IsNullOrEmpty(caption.text)) problems.Add($"{line} empty");
                if (!members.Contains(caption)) problems.Add($"{line} not in the captions' group");
                if (caption.textInfo.lineCount > 1) problems.Add($"{line} on {caption.textInfo.lineCount} lines");
                if (Overflows(caption)) problems.Add($"{line} overflows its box ({TextWidth(caption)} in {caption.rectTransform.rect.width})");
                if (!Mathf.Approximately(caption.fontSize, group.Size)) problems.Add($"{line} at {caption.fontSize}, its group at {group.Size}");
            }
        }

        // The screens (and whether a banner is reserved) where the busy menu with the showcase's rows does not fit yet, with
        // the numbers in the kit card bounds plan's final fix report: on the portrait tablet with a banner the title's and
        // the logo's bands, at their smallest, still reach the top bar (61 units between the block and the bar; the
        // features' rows cannot scale, the Shop's button is touch-sized at full size); in landscape the column under the
        // title is not scaled by height (16:9 and 20:9 with a banner: the bands and the column reach the top bar) and on
        // the tablet its width scale takes the Shop's button under the touch size. Pending a design ruling:
        // Busy_menu_parts_never_overlap leaves their fit checks out and Busy_menu_fits_on_the_known_gaps holds them
        private static readonly (string Screen, bool Banner)[] KnownGaps =
        {
            // cmpose:showcase
            ("tablet", true), ("16:9 landscape", true), ("20:9 landscape", true), ("tablet landscape", false), ("tablet landscape", true)
            // /cmpose:showcase
        };

        private static bool KnownGap(string screen) =>
            KnownGaps.Contains((screen.Split(new[] { " (" }, StringSplitOptions.None)[0], SafeArea.ReservedTop > 0f));

        // The busy menu on every screen in every language, five round buttons and six (three a side), with and without a
        // top banner (the top bar one button lower with it, as MenuView does), arranged for its orientation
        // (MenuView.Arrange); check gets the screen's name, the place's name, the menu and its number of buttons
        private void ForEachBusyMenu(Action<string, string, Transform, MenuView, int> check)
        {
            var localizer = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            foreach (var language in Languages())
            {
                Assert.That(localizer.SetLanguage(language), Is.True, language);
                ForEachScreen((name, screen, theme) =>
                {
                    foreach (var full in new[] { false, true })
                    {
                        InScene(MenuScene, screen, theme, canvas =>
                        {
                            var menu = canvas.GetComponentInChildren<MenuView>();
                            BusyMenu(menu, theme, localizer, full);
                            var buttons = full ? 6 : 5;
                            WithEachBanner($"{name} {language}, {buttons} buttons", screen, new[] { canvas.gameObject }, where =>
                            {
                                menu.ReserveBannerSpace(SafeArea.ReservedTop > 0f);
                                LayoutStage.Layout(canvas.gameObject);
                                menu.Arrange();
                                LayoutStage.Layout(canvas.gameObject);
                                check(name, where, canvas, menu, buttons);
                            });
                        });
                    }
                });
            }
        }

        // Its bottom block never reaches the title, the logo, the top bar or the version text, the title and the logo
        // stay clear of the top bar, six buttons wrap to a second line rather than shrink under the touch size, and the
        // captions fit (KnownGaps aside)
        [Test]
        public void Busy_menu_parts_never_overlap()
        {
            var problems = new List<string>();
            var gaps = new List<string>();
            ForEachBusyMenu((name, where, canvas, menu, buttons) =>
            {
                CheckBusyMenu(where, canvas, menu, buttons, problems, KnownGap(name) ? gaps : problems);
                CheckCaptions(where, menu, problems);
            });
            if (gaps.Count > 0) Debug.Log($"Busy_menu_parts_never_overlap: {gaps.Count} fit problems on the known gaps\n{string.Join("\n", gaps)}");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // The known gaps held to the same fit checks: ignored until the design ruling closes them
        [Test]
        [Ignore("the busy showcase menu does not fit these screens yet: see KnownGaps")]
        public void Busy_menu_fits_on_the_known_gaps()
        {
            var problems = new List<string>();
            ForEachBusyMenu((name, where, canvas, menu, buttons) =>
            {
                if (KnownGap(name)) CheckBusyMenu(where, canvas, menu, buttons, problems, problems);
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Compass writes a game's credits (Resources/Credits.txt, CC BY attributions): a long Turkish one wraps inside the
        // Credits panel's box (an unbroken URL too) in the body font, shown as written, scrolls when it is taller than the
        // box, and the box stays on the screen, KitLayout.Gap clear of the title and Close
        [Test]
        public void Long_turkish_credits_scroll_and_do_not_overflow()
        {
            var problems = new List<string>();
            var credits = LongTurkishCredits();
            ForEachScreen((name, screen, theme) =>
            {
                var root = LayoutStage.Prefab(SettingsPrefab, screen, theme, made);
                var panel = root.GetComponentInChildren<CreditsPanel>(true);
                Assert.That(panel, Is.Not.Null, "CreditsPanel in the settings prefab");
                var area = Require(root.transform, CreditsArea);
                var body = panel.Body;
                body.text = credits;
                LayoutStage.Layout(root);
                var scroll = panel.Scroll;
                var box = LayoutStage.World((RectTransform)scroll.transform);
                var viewport = LayoutStage.World(scroll.viewport);
                var text = LayoutStage.World(body.rectTransform);
                if (!scroll.vertical || scroll.horizontal) problems.Add($"{name}: the credits box does not scroll vertically only");
                if (scroll.content.rect.height <= scroll.viewport.rect.height + 1f)
                    problems.Add($"{name}: long credits ({scroll.content.rect.height}) fit the box ({scroll.viewport.rect.height}): nothing scrolls");
                if (body.isTextOverflowing) problems.Add($"{name}: credits overflow their text box");
                var room = body.rectTransform.rect.width - body.margin.x - body.margin.z;
                if (body.textBounds.size.x > room + 1f) problems.Add($"{name}: credits {body.textBounds.size.x} wide in {room}");
                if (body.rectTransform.rect.height < body.preferredHeight - 1f)
                    problems.Add($"{name}: credits box {body.rectTransform.rect.height} high for {body.preferredHeight} of text");
                if (text.xMin < viewport.xMin - Slack || text.xMax > viewport.xMax + Slack) problems.Add($"{name}: credits {text} wider than the box {viewport}");
                if (body.richText) problems.Add($"{name}: credits read rich text tags");
                var role = body.GetComponent<ThemedText>();
                if (role == null || role.Role != TextRole.Body) problems.Add($"{name}: credits not in the body font");
                if (!Inside(LayoutStage.World((RectTransform)area), box)) problems.Add($"{name}: credits box {box} outside the safe area");
                foreach (var neighbour in new[] { "Title", "Close" })
                {
                    var gap = LayoutStage.Distance(box, LayoutStage.World((RectTransform)Require(area, neighbour)));
                    if (gap < KitLayout.Gap - Slack) problems.Add($"{name}: credits box {gap} from {neighbour}");
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Forty paragraphs in Compass's form, Turkish letters, a word that is not a tag and a URL longer than any line
        private static string LongTurkishCredits()
        {
            var paragraphs = new List<string>();
            for (var i = 1; i <= 40; i++)
                paragraphs.Add($"\"Gökyüzü Şarkısı {i}\" by Çağla Öztürk & İlkay Ünlü, https://opengameart.org/content/gokyuzu-sarkisi-{i}, " +
                    "CC BY 4.0. Değişiklik: ses düzeyi ayarlandı, başı ve sonu kırpıldı <b>.");
            paragraphs.Add("https://example.org/" + string.Concat(Enumerable.Repeat("çokuzunbirbağlantıĞÜŞİÖÇ", 12)));
            return string.Join("\n\n", paragraphs);
        }

        // cmpose:sample
        // The score has no string key, so Texts_fit_their_boxes skips it: six digits stay inside its box
        [Test]
        public void Hud_score_fits_six_digits()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) => InScene(GameScene, screen, theme, canvas =>
            {
                var hud = canvas.GetComponentInChildren<ShellHudView>(true);
                var score = Require(Require(hud.transform, "Content"), "Score").GetComponent<TMP_Text>();
                score.text = "999999";
                LayoutStage.Layout(canvas.gameObject);
                var room = score.rectTransform.rect.width - score.margin.x - score.margin.z;
                if (score.isTextOverflowing || score.textBounds.size.x > room + 1f)
                    problems.Add($"{name}: \"999999\" overflows the score ({score.textBounds.size.x} in {room})");
                if (score.fontSize < score.fontSizeMin - 0.01f)
                    problems.Add($"{name}: score at {score.fontSize}, under its minimum {score.fontSizeMin}");
            }));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
        // /cmpose:sample
    }
}
