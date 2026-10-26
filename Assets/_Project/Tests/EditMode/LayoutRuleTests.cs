using System;
using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
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
    // The kit's layout rules on the built screens (spec §4.4), on every LayoutStage screen, without a skin and with a
    // version 2 skin whose frames are thick: round icons centred on their face, settings in two columns, the settings
    // card on the screen, HUD items apart, touch targets of 48 dp that never overlap, texts inside their boxes.
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
        // The settings rows: two columns each, the label left and the control right
        private static readonly string[] SettingsRows = { "Music", "Sfx", "Vibration", "Section0", "Section1", "Section2", "Section3" };
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

        // Every screen without a skin and with the thick-frame skin; check gets the screen's name and a theme
        private void ForEachScreen(Action<string, Vector2, IThemeSource> check)
        {
            foreach (var skinned in new[] { false, true })
            {
                GameSkin.ForTests(skinned ? ThickSkin() : GameSkin.Parse("{}", _ => null, _ => null));
                foreach (var (name, screen) in LayoutStage.Screens)
                    check(skinned ? $"{name} (skin)" : name, screen, Theme());
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

        private FakeThemeSource Theme()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            return new FakeThemeSource { Current = palette, Style = ThemeStyle.Load() };
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
                var glyph = kit.Icon != null && kit.Icon.isActiveAndEnabled && kit.Icon.sprite != null ? kit.Icon.rectTransform : kit.Label.rectTransform;
                var off = In(space, glyph).center - (In(space, kit.Body.rectTransform).center + kit.ContentOffset);
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

        [Test]
        public void Settings_rows_share_two_columns()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) =>
            {
                var card = (RectTransform)Require(LayoutStage.Prefab(SettingsPrefab, screen, theme, made).transform, SettingsCard);
                var padding = card.GetComponent<KitCard>().ContentPadding;
                var box = card.rect;
                var content = Rect.MinMaxRect(box.xMin + padding.left, box.yMin + padding.bottom, box.xMax - padding.right, box.yMax - padding.top);
                float? labelStart = null, controlEnd = null, sliderWidth = null;
                foreach (var rowName in SettingsRows)
                {
                    var row = (RectTransform)Require(card, rowName);
                    var label = (RectTransform)(row.Find("Label") ?? row.Find("Title"));
                    var control = (RectTransform)(row.GetComponentInChildren<Selectable>(true).transform);
                    if (label == null)
                    {
                        problems.Add($"{name}: {rowName} has no label");
                        continue;
                    }
                    var rowRect = In(card, row);
                    var l = In(card, label);
                    var c = In(card, control);
                    labelStart = labelStart ?? l.xMin;
                    controlEnd = controlEnd ?? c.xMax;
                    if (Mathf.Abs(l.xMin - labelStart.Value) > Slack) problems.Add($"{name}: {rowName} label starts at {l.xMin}, not {labelStart}");
                    if (Mathf.Abs(c.xMax - controlEnd.Value) > Slack) problems.Add($"{name}: {rowName} control ends at {c.xMax}, not {controlEnd}");
                    var start = rowRect.xMin + SettingsView.ControlStart * rowRect.width;
                    var isSwitch = control.GetComponent<Toggle>() != null;
                    if (isSwitch ? c.xMin < start - Slack : Mathf.Abs(c.xMin - start) > Slack)
                        problems.Add($"{name}: {rowName} control starts at {c.xMin}, not at the control column ({start})");
                    if (l.xMax > rowRect.xMin + SettingsView.LabelShare * rowRect.width + Slack) problems.Add($"{name}: {rowName} label crosses its column");
                    if (control.GetComponent<Slider>() != null)
                    {
                        sliderWidth = sliderWidth ?? c.width;
                        if (Mathf.Abs(c.width - sliderWidth.Value) > Slack) problems.Add($"{name}: {rowName} is {c.width} wide, not {sliderWidth}");
                    }
                    if (!Inside(content, l)) problems.Add($"{name}: {rowName} label {l} outside the card's content {content}");
                    if (!Inside(content, c)) problems.Add($"{name}: {rowName} control {c} outside the card's content {content}");
                }
            });
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Settings_card_fits_the_screen_height()
        {
            var problems = new List<string>();
            ForEachScreen((name, screen, theme) =>
            {
                var root = LayoutStage.Prefab(SettingsPrefab, screen, theme, made).transform;
                var card = LayoutStage.World((RectTransform)Require(root, SettingsCard));
                var safe = LayoutStage.World((RectTransform)Require(root, "Panel/SafeArea"));
                if (!Inside(safe, card)) problems.Add($"{name}: card {card} outside the safe area {safe}");
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
            var languages = new List<string> { "en" };
            // cmpose:multiLang
            languages.AddRange(new[] { "tr", "fr" });
            // /cmpose:multiLang
            foreach (var language in languages)
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
                    InScene(MenuScene, screen, theme, canvas => Check($"{name} Menu", canvas.gameObject));
                    // cmpose:sample
                    InScene(GameScene, screen, theme, canvas => Check($"{name} Game", canvas.gameObject));
                    // /cmpose:sample
                });
            }
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
