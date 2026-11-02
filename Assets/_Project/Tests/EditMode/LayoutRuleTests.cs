using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.Settings;
// cmpose:multiLang
using Kturker.UnityBase.Core.Language;
// /cmpose:multiLang
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
// cmpose:purchases
using Kturker.UnityBase.Core.Purchases;
// /cmpose:purchases
// cmpose:sample
using Kturker.UnityBase.Features.Shell.GameShell;
// /cmpose:sample
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The kit's layout rules on the built screens (spec §4.4), on every LayoutStage screen, without a skin, with a
    // version 2 skin whose frames are thick and in the tallest font pair (LayoutStage.TallStyle): round icons centred on their
    // face, the busiest settings card in every language (one-line texts, one control column, an equal button grid,
    // touch-sized controls on the screen, one primary, Close top right, the language page), HUD items apart, touch
    // targets of 48 dp that never overlap, texts inside their boxes.
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
        }

        // Every screen in the app's style without a skin and with the thick-frame skin, and in the tall font pair
        // (LayoutStage.TallStyle) without a skin: the app's own style has the fonts of one family only, and a game may
        // pick the pair the kit's sizes leave the least room for (the skin brings its own font); check gets the screen's
        // name and a theme
        private void ForEachScreen(Action<string, Vector2, IThemeSource> check)
        {
            var own = ThemeStyle.Load();
            var tall = LayoutStage.TallStyle(made);
            foreach (var (variant, skinned, style) in new[] { ("", false, own), (" (skin)", true, own), (" (Baloo 2)", false, tall) })
            {
                GameSkin.ForTests(skinned ? ThickSkin() : GameSkin.Parse("{}", _ => null, _ => null));
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
            return text.isTextOverflowing || text.textBounds.size.x > room + 1f;
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
                    // The menu's side button is a template code copies into the side columns
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
                        if (text.isTextOverflowing || text.textBounds.size.x > room + 1f)
                            problems.Add($"{path} overflows its box ({text.textBounds.size.x} in {room})");
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

        // Each economy window's band of the safe area's height before it was fitted (SceneBuilder's EconomyWindow calls)
        private static readonly (string Window, float YMin, float YMax)[] EconomyBands =
        {
            ("Supply", 0.14f, 0.86f), ("Wheel", 0.1f, 0.9f), ("Login", 0.12f, 0.88f), ("Extra", 0.18f, 0.82f), ("Picker", 0.18f, 0.82f),
            ("Offer", 0.22f, 0.78f)
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
