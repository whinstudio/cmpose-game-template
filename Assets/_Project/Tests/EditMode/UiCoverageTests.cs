using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Tests.EditMode
{
    // SceneBuilder is the only writer of scenes and prefabs; these checks keep its output honest
    public sealed class UiCoverageTests
    {
        private static readonly string[] Prefabs =
        {
            "Assets/_Project/Core/DI/ProjectLifetimeScope.prefab",
            "Assets/_Project/App/Settings/SettingsPanel.prefab",
            "Assets/_Project/Features/Economy/Resources/Economy/EconomyWindows.prefab",
            // cmpose:sample
            "Assets/_Project/Features/Sample/Resources/Sample/SampleView.prefab",
            // /cmpose:sample
            // cmpose:network
            // cmpose:sample
            "Assets/_Project/Features/Sample/Network/Resources/SampleNetwork/GitHubUserCard.prefab",
            // /cmpose:sample
            // /cmpose:network
            // cmpose:networkInspector
            "Assets/_Project/Core/NetworkInspector/Resources/NetworkInspector/InspectorOverlay.prefab",
            // /cmpose:networkInspector
            // cmpose:purchases
            "Assets/_Project/Features/Shell/Purchases/Resources/ShellPurchases/Paywall.prefab",
            // /cmpose:purchases
            // cmpose:ads
            // cmpose:sample
            "Assets/_Project/Features/Sample/Ads/Resources/SampleAds/Bonus.prefab",
            // /cmpose:sample
            // /cmpose:ads
            // cmpose:leaderboards
            // cmpose:sample
            "Assets/_Project/Features/Sample/Leaderboards/Resources/SampleLeaderboards/Scores.prefab",
            // /cmpose:sample
            // /cmpose:leaderboards
            // cmpose:showcase
            "Assets/_Project/Features/Showcase/Common/Resources/ShowcaseCommon/MenuBest.prefab",
            // cmpose:ads
            "Assets/_Project/Features/Showcase/Common/Ads/Resources/ShowcaseAds/Continue.prefab",
            // /cmpose:ads
            // cmpose:purchases
            "Assets/_Project/Features/Showcase/Common/Purchases/Resources/ShowcasePurchases/Premium.prefab",
            // /cmpose:purchases
            // cmpose:leaderboards
            "Assets/_Project/Features/Showcase/Common/Leaderboards/Resources/ShowcaseLeaderboards/Scores.prefab",
            // /cmpose:leaderboards
            // cmpose:saveSystem
            "Assets/_Project/Features/Showcase/Common/Save/Resources/ShowcaseSave/Stats.prefab",
            // /cmpose:saveSystem
            // /cmpose:showcase
        };

        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Boot.unity",
            "Assets/_Project/Scenes/Menu.unity",
            // cmpose:sample
            "Assets/_Project/Scenes/Game.unity",
            // /cmpose:sample
            // cmpose:runner2D
            "Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity",
            // /cmpose:runner2D
            // cmpose:track3D
            "Assets/_Project/Features/Showcase/Track3D/Scenes/Track3D.unity",
            // /cmpose:track3D
        };

        private static void ForEachRoot(Action<string, GameObject> check)
        {
            foreach (var path in Prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                check(path, prefab);
            }
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects()) check(path, root);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";

        [Test]
        public void Every_text_has_a_key_or_is_written_by_code_and_every_key_is_in_english()
        {
            var english = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    var localized = text.GetComponent<LocalizedText>();
                    if (localized == null)
                    {
                        problems.Add($"{where}: {PathOf(text.transform)} has no LocalizedText");
                    }
                    else if (!string.IsNullOrEmpty(localized.Key) && english.Get(localized.Key) == localized.Key)
                    {
                        problems.Add($"{where}: {PathOf(text.transform)} key '{localized.Key}' is not in Strings/en");
                    }
                }
            });
            Assert.That(problems, Is.Empty);
        }

        private const string SettingsPrefab = "Assets/_Project/App/Settings/SettingsPanel.prefab";
        private const string EconomyPrefab = "Assets/_Project/Features/Economy/Resources/Economy/EconomyWindows.prefab";
        // The settings modal's card: a column that grows with the rows the presenter shows
        private const string SettingsColumn = "Panel/SafeArea/Card";
        // cmpose:networkInspector
        private const string InspectorPrefab = "Assets/_Project/Core/NetworkInspector/Resources/NetworkInspector/InspectorOverlay.prefab";
        // /cmpose:networkInspector
        // cmpose:purchases
        private const string PaywallPrefab = "Assets/_Project/Features/Shell/Purchases/Resources/ShellPurchases/Paywall.prefab";
        // /cmpose:purchases

        // The 1080x1920 reference and a 20:9 phone (1080x2400): the canvas scaler shows the taller one about 57 fewer
        // units on each side, so a box placed for the reference can end up near the screen's edge
        private static readonly Vector2Int[] Phones = { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400) };

        // Content keeps at least this many screen pixels from the screen's sides
        private const float SideMargin = 48f;

        private static GameObject Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        // A phone screen without a canvas: screen roots are laid out on a stage the size the canvas scaler gives them,
        // every child shown (rows the presenter shows count), so rects read in canvas units and convert to pixels
        private sealed class Stage : IDisposable
        {
            private readonly GameObject root;

            public Stage(Vector2Int phone)
            {
                Phone = phone;
                var scaler = Load(SettingsPrefab).GetComponent<CanvasScaler>();
                var reference = scaler.referenceResolution;
                var log = Mathf.Lerp(Mathf.Log(phone.x / reference.x, 2f), Mathf.Log(phone.y / reference.y, 2f), scaler.matchWidthOrHeight);
                Scale = Mathf.Pow(2f, log);
                Size = new Vector2(phone.x / Scale, phone.y / Scale);
                root = new GameObject("Stage", typeof(RectTransform));
                ((RectTransform)root.transform).sizeDelta = Size;
            }

            public Vector2Int Phone { get; }
            public float Scale { get; }
            public Vector2 Size { get; }

            // A copy of a screen root (prefab or scene object), filling the stage or put into a slot the way the
            // runtime puts it there (Instantiate(prefab, slot, false)); its own canvas goes, the stage is the canvas
            public GameObject Add(GameObject source, Transform slot = null)
            {
                var copy = UnityEngine.Object.Instantiate(source, slot != null ? slot : root.transform, false);
                foreach (var c in copy.GetComponents<CanvasScaler>()) UnityEngine.Object.DestroyImmediate(c);
                foreach (var c in copy.GetComponents<GraphicRaycaster>()) UnityEngine.Object.DestroyImmediate(c);
                foreach (var c in copy.GetComponents<Canvas>()) UnityEngine.Object.DestroyImmediate(c);
                if (slot == null)
                {
                    var rect = (RectTransform)copy.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
                foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
                Layout();
                return copy;
            }

            // A scene's object (path from its root) copied onto the stage; the scene is closed again
            public GameObject AddFromScene(string scenePath, string objectPath)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var names = objectPath.Split('/');
                    foreach (var top in scene.GetRootGameObjects())
                    {
                        if (top.name != names[0]) continue;
                        var found = names.Length == 1 ? top.transform : top.transform.Find(string.Join("/", names, 1, names.Length - 1));
                        if (found != null) return Add(found.gameObject);
                    }
                    Assert.Fail($"{scenePath}: no {objectPath}");
                    return null;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            // Layout groups lay out their own children only, so each one is rebuilt, outermost first
            private void Layout()
            {
                for (var pass = 0; pass < 2; pass++)
                {
                    foreach (var group in root.GetComponentsInChildren<LayoutGroup>(true))
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
                }
            }

            // A rect in stage units, (0, 0) at the screen's bottom left
            public Rect RectOf(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var min = root.transform.InverseTransformPoint(corners[0]);
                var max = root.transform.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(min.x + Size.x / 2f, min.y + Size.y / 2f, max.x + Size.x / 2f, max.y + Size.y / 2f);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }

        // The screens with their side margin in screen pixels
        private static List<(string Where, GameObject Root, float Margin)> Screens(Stage stage)
        {
            const string menu = "Assets/_Project/Scenes/Menu.unity";
            var screens = new List<(string, GameObject, float)>
            {
                ("Settings", stage.Add(Load(SettingsPrefab)), SideMargin),
                ("Boot", stage.AddFromScene("Assets/_Project/Scenes/Boot.unity", "Canvas/SafeArea"), SideMargin),
                ("Menu", StageMenu(stage), SideMargin),
                ("Menu/About", stage.AddFromScene(menu, "Canvas/About"), SideMargin),
                ("Menu/Update", stage.AddFromScene(menu, "UpdatePanel"), SideMargin),
                ("Economy", stage.Add(Load(EconomyPrefab)), SideMargin)
            };
            // cmpose:networkInspector
            screens.Add(("Inspector", stage.Add(Load(InspectorPrefab)), SideMargin));
            // /cmpose:networkInspector
            // cmpose:purchases
            screens.Add(("Paywall", stage.Add(Load(PaywallPrefab)), SideMargin));
            // /cmpose:purchases
            // cmpose:sample
            screens.Add(("Game/Sample", StageSample(stage), SideMargin));
            screens.Add(("Game/Guide", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/Guide"), SideMargin));
            screens.Add(("Game/Pause", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/Pause"), SideMargin));
            screens.Add(("Game/ShellHud", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/SafeArea/ShellHud"), SideMargin));
            screens.Add(("Game/StartHint", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/SafeArea/StartHint"), SideMargin));
            screens.Add(("Game/ShellGameOver", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/ShellGameOver"), SideMargin));
            screens.Add(("Game/LevelComplete", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/LevelComplete"), SideMargin));
            screens.Add(("Game/LevelFailed", stage.AddFromScene("Assets/_Project/Scenes/Game.unity", "Canvas/LevelFailed"), SideMargin));
            // /cmpose:sample
            // cmpose:runner2D
            screens.Add(("Runner2D/Hud", stage.AddFromScene("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity", "Canvas/SafeArea/Hud"), SideMargin));
            screens.Add(("Runner2D/Pause", stage.AddFromScene("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity", "Canvas/Pause"), SideMargin));
            screens.Add(("Runner2D/GameOver", StageGameOver(stage), SideMargin));
            // /cmpose:runner2D
            return screens;
        }

        // The menu with the rows the showcase's flags put into its extra slot (a column) at runtime
        private static GameObject StageMenu(Stage stage)
        {
            var menu = stage.AddFromScene("Assets/_Project/Scenes/Menu.unity", "Canvas/SafeArea/Menu");
            var extras = menu.transform.Find("Extras");
            // cmpose:showcase
            stage.Add(Load("Assets/_Project/Features/Showcase/Common/Resources/ShowcaseCommon/MenuBest.prefab"), extras);
            // cmpose:purchases
            stage.Add(Load("Assets/_Project/Features/Showcase/Common/Purchases/Resources/ShowcasePurchases/Premium.prefab"), extras);
            // /cmpose:purchases
            // cmpose:saveSystem
            stage.Add(Load("Assets/_Project/Features/Showcase/Common/Save/Resources/ShowcaseSave/Stats.prefab"), extras);
            // /cmpose:saveSystem
            // cmpose:leaderboards
            stage.Add(Load("Assets/_Project/Features/Showcase/Common/Leaderboards/Resources/ShowcaseLeaderboards/Scores.prefab"), extras);
            // /cmpose:leaderboards
            // /cmpose:showcase
            return menu;
        }

        // cmpose:runner2D
        // The showcase's game-over panel (the same in both games) with the Continue row the ads flag puts in its slot
        private static GameObject StageGameOver(Stage stage)
        {
            var gameOver = stage.AddFromScene("Assets/_Project/Features/Showcase/Runner2D/Scenes/Runner2D.unity", "Canvas/GameOver");
            // cmpose:ads
            stage.Add(Load("Assets/_Project/Features/Showcase/Common/Ads/Resources/ShowcaseAds/Continue.prefab"), gameOver.transform.Find("Panel/SafeArea/Card/Content/Actions"));
            // /cmpose:ads
            return gameOver;
        }
        // /cmpose:runner2D

        // cmpose:sample
        // The Game scene's sample panel (ModuleSlot makes it screen-sized) with what the flags put into its slots at
        // runtime: the GitHub card (Extras), Leaderboard and Achievements (Scores), the Bonus button (Actions)
        private static GameObject StageSample(Stage stage)
        {
            var sample = stage.Add(Load("Assets/_Project/Features/Sample/Resources/Sample/SampleView.prefab"));
            // cmpose:network
            stage.Add(Load("Assets/_Project/Features/Sample/Network/Resources/SampleNetwork/GitHubUserCard.prefab"), sample.transform.Find("Extras"));
            // /cmpose:network
            // cmpose:leaderboards
            stage.Add(Load("Assets/_Project/Features/Sample/Leaderboards/Resources/SampleLeaderboards/Scores.prefab"), sample.transform.Find("Scores"));
            // /cmpose:leaderboards
            // cmpose:ads
            stage.Add(Load("Assets/_Project/Features/Sample/Ads/Resources/SampleAds/Bonus.prefab"), sample.transform.Find("Actions"));
            // /cmpose:ads
            return sample;
        }
        // /cmpose:sample

        [Test]
        public void Screens_keep_clear_of_the_sides_on_the_reference_and_a_20_by_9_phone()
        {
            var problems = new List<string>();
            foreach (var phone in Phones)
            {
                using (var stage = new Stage(phone))
                {
                    foreach (var (where, screen, margin) in Screens(stage))
                    {
                        foreach (var rect in screen.GetComponentsInChildren<RectTransform>(true))
                        {
                            // A kit box's soft shadow is blur around the box, not content: the box itself is checked
                            if (IsKitShadow(rect)) continue;
                            // Background shapes (KitDecor) are not content: they may run off the screen's sides
                            if (rect.GetComponent<KitDecor>() != null) continue;
                            var r = stage.RectOf(rect);
                            // A rect as wide as the screen is a full-screen layer (panel, safe area, module scroll)
                            if (r.xMin <= 0.5f && r.xMax >= stage.Size.x - 0.5f) continue;
                            var left = r.xMin * stage.Scale;
                            var right = (stage.Size.x - r.xMax) * stage.Scale;
                            if (left < margin - 0.5f || right < margin - 0.5f)
                                problems.Add($"{phone.x}x{phone.y} {where}: {PathOf(rect)} is {left:0} px / {right:0} px from the sides");
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty);
        }

        private static bool IsKitShadow(Transform t)
        {
            var themed = t.GetComponent<ThemedGraphic>();
            return t.name == "Shadow" && themed != null && themed.Role == ThemeRole.Scrim;
        }

        // cmpose:networkInspector
        // The debug NET button sits in the top right corner and covers no other screen's button or text box (the
        // paywall's buttons and title, the Game scene's bottom controls and pause button, the menu's and About's titles)
        [Test]
        public void The_net_button_sits_top_right_clear_of_every_other_button_and_text()
        {
            var problems = new List<string>();
            foreach (var phone in Phones)
            {
                using (var stage = new Stage(phone))
                {
                    var screens = Screens(stage);
                    var inspector = screens.Find(s => s.Where == "Inspector").Root;
                    var net = stage.RectOf((RectTransform)inspector.transform.Find("SafeArea/Open"));
                    Assert.That(net.center.x, Is.GreaterThan(stage.Size.x / 2f), $"{phone}: NET is not on the right");
                    Assert.That(net.center.y, Is.GreaterThan(stage.Size.y * 0.85f), $"{phone}: NET is not at the top");
                    // Clear of the screen's top edge on a phone without a cutout (no safe-area inset)
                    Assert.That((stage.Size.y - net.yMax) * stage.Scale, Is.GreaterThanOrEqualTo(24f - 0.5f), $"{phone}: NET top gap");
                    foreach (var (where, screen, _) in screens)
                    {
                        if (screen == inspector) continue;
                        foreach (var button in screen.GetComponentsInChildren<Button>(true))
                        {
                            if (stage.RectOf((RectTransform)button.transform).Overlaps(net))
                                problems.Add($"{phone.x}x{phone.y} {where}: NET covers {PathOf(button.transform)}");
                        }
                        foreach (var text in screen.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (stage.RectOf(text.rectTransform).Overlaps(net))
                                problems.Add($"{phone.x}x{phone.y} {where}: NET covers text {PathOf(text.transform)}");
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty);
        }
        // /cmpose:networkInspector

        // cmpose:purchases
        // Package rows start on Track, which stands out from the paywall's Surface panel in both palettes;
        // PaywallView.Select gives the chosen one SurfaceVariant (ThemeTests' contrast checks)
        [Test]
        public void Paywall_rows_start_on_the_track_role()
        {
            var paywall = Load(PaywallPrefab);
            for (var i = 0; ; i++)
            {
                var row = paywall.transform.Find($"Panel/SafeArea/Plans/Row{i}");
                if (row == null)
                {
                    Assert.That(i, Is.GreaterThan(0), "no paywall rows");
                    break;
                }
                Assert.That(row.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Track), row.name);
            }
        }

        // The welcome bundle's line (IPaywallBonus) is wired, hidden until bound and sits under the packages, above the
        // status line; the presenter writes it whenever packages show
        [Test]
        public void Paywall_bonus_line_sits_under_the_packages()
        {
            var paywall = Load(PaywallPrefab);
            var view = paywall.GetComponent<Kturker.UnityBase.Features.Shell.Purchases.PaywallView>();
            var bonus = new SerializedObject(view).FindProperty("bonus").objectReferenceValue as LocalizedText;
            Assert.That(bonus, Is.Not.Null, "PaywallView.bonus is wired");
            Assert.That(bonus.gameObject.activeSelf, Is.False, "hidden until a bonus is bound");
            var plans = paywall.transform.Find("Panel/SafeArea/Plans");
            Assert.That(bonus.transform.parent, Is.SameAs(plans), "in the packages' column");
            var lastRow = plans.Find($"Row{Kturker.UnityBase.Features.Shell.Purchases.PaywallView.Rows - 1}");
            Assert.That(bonus.transform.GetSiblingIndex(), Is.GreaterThan(lastRow.GetSiblingIndex()), "under the packages");
            Assert.That(bonus.transform.GetSiblingIndex(), Is.LessThan(plans.Find("Status").GetSiblingIndex()), "above the status line");
        }
        // /cmpose:purchases

        // Slider rails take the Control role (the Primary fill reads on it in both palettes); the vibration row is a kit
        // switch, whose track turns Control when off (KitTests)
        [Test]
        public void Settings_controls_use_the_control_role()
        {
            var column = Load(SettingsPrefab).transform.Find(SettingsColumn);
            foreach (var path in new[] { "Music/Rail", "Sfx/Rail" })
                Assert.That(column.Find(path).GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control), path);
            Assert.That(column.Find("Vibration/Switch").GetComponent<KitSwitch>(), Is.Not.Null, "vibration switch");
        }

        // The vibration row reads like the other settings rows: its label left-aligned where their titles start, its
        // switch in the share where their value buttons sit
        [Test]
        public void The_vibration_row_lines_up_with_the_other_settings_rows()
        {
            foreach (var phone in Phones)
            {
                using (var stage = new Stage(phone))
                {
                    var column = stage.Add(Load(SettingsPrefab)).transform.Find(SettingsColumn);
                    var label = column.Find("Vibration/Label").GetComponent<TMP_Text>();
                    var title = column.Find("Section0/Title").GetComponent<TMP_Text>();
                    var labelRect = stage.RectOf(label.rectTransform);
                    var titleRect = stage.RectOf(title.rectTransform);
                    Assert.That(label.alignment, Is.EqualTo(title.alignment), $"{phone}: alignment");
                    Assert.That(labelRect.xMin, Is.EqualTo(titleRect.xMin).Within(1f), $"{phone}: label start");
                    var box = stage.RectOf((RectTransform)column.Find("Vibration/Switch"));
                    var value = stage.RectOf((RectTransform)column.Find("Section0/Value"));
                    Assert.That(box.center.x, Is.EqualTo(value.center.x).Within(1f), $"{phone}: switch in the value share");
                }
            }
        }

        // cmpose:network
        // cmpose:sample
        // The GitHub card stands out from the Game screen's background in both palettes (ThemeTests' contrast checks)
        [Test]
        public void The_github_card_uses_the_surface_variant_role()
        {
            var card = Load("Assets/_Project/Features/Sample/Network/Resources/SampleNetwork/GitHubUserCard.prefab");
            Assert.That(card.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.SurfaceVariant));
        }
        // /cmpose:sample
        // /cmpose:network

        // Labels code writes at runtime, by button name: the keys (measured in every language) and the literal texts
        // the settings rows' value buttons, the menu's game buttons (also "Level n") and the end cards' Continue buttons can
        // show
        private static (Dictionary<string, List<string>> Keys, Dictionary<string, List<string>> Texts) RuntimeLabels()
        {
            var keys = new Dictionary<string, List<string>>
            {
                ["Value"] = new List<string>(),
                ["Play"] = new List<string> { "menu.play", "shell.level" },
                ["ContinueButton"] = new List<string> { "shell.continue", "shell.continueAd" },
                // The booster offer's price next to the currency's icon
                ["OfferBuy"] = new List<string> { "economy.price" }
            };
            var texts = new Dictionary<string, List<string>>();
            // cmpose:uiTheme
            keys["Value"].AddRange(new[] { "theme.system", "theme.light", "theme.dark" });
            // /cmpose:uiTheme
            // cmpose:multiLang
            texts["Value"] = Kturker.UnityBase.Core.Language.LanguageSelector.Supported.Select(l => l.Name).ToList();
            // /cmpose:multiLang
            // cmpose:purchases
            keys["Value"].AddRange(new[] { "purchases.state.get", "purchases.state.active", "purchases.state.unavailable" });
            // /cmpose:purchases
            // cmpose:leaderboards
            keys["Value"].AddRange(new[] { "leaderboards.signIn", "leaderboards.signedIn", "leaderboards.notSetUp" });
            // /cmpose:leaderboards
            // cmpose:ads
            keys["Value"].Add("ads.privacy.manage");
            // /cmpose:ads
            // cmpose:runner2D
            keys["Play"].Add("runner2d.play");
            // /cmpose:runner2D
            // cmpose:track3D
            keys["Play"].Add("track3d.play");
            // /cmpose:track3D
            // cmpose:showcase
            // cmpose:ads
            keys["ContinueButton"].Add("showcase.continue");
            // /cmpose:ads
            // /cmpose:showcase
            return (keys, texts);
        }

        // Every language table's text of every button label key fits its button at the label's smallest auto-size
        // (KitFactory.Button: 30) without the ellipsis: whole words on as many lines as the label's height holds, on the
        // narrower phone too (buttons that stretch with the screen are narrower there)
        [Test]
        public void Button_labels_fit_in_every_language_at_the_smallest_size()
        {
            var languages = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => Regex.Match(p, "/Resources/Strings/([^/]+)/"))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();
            Assert.That(languages, Does.Contain("en"));
            var tables = languages.ToDictionary(l => l, l =>
            {
                var table = new TableLocalizer(new ResourcesStringSource(), new FakeLog(), false);
                table.SetLanguage(l);
                return table;
            });
            var (runtimeKeys, runtimeTexts) = RuntimeLabels();
            foreach (var key in runtimeKeys.Values.SelectMany(k => k))
                Assert.That(tables["en"].Get(key), Is.Not.EqualTo(key), $"runtime label key '{key}' is not in Strings/en");
            var problems = new HashSet<string>();
            foreach (var phone in Phones)
            {
                using (var stage = new Stage(phone))
                {
                    var roots = Screens(stage).Select(s => (s.Where, s.Root)).ToList();
                    foreach (var path in Prefabs)
                    {
                        var prefab = Load(path);
                        if (prefab.transform is RectTransform) roots.Add((path, stage.Add(prefab)));
                    }
                    foreach (var path in Scenes)
                    {
                        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        try
                        {
                            foreach (var top in scene.GetRootGameObjects())
                                if (top.transform is RectTransform) roots.Add((path, stage.Add(top)));
                        }
                        finally
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                    var probe = new GameObject("Probe", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    // Measured with an in-memory dynamic copy of each label font's source file (the same glyph metrics,
                    // every language's letters), so the run never adds glyphs to the project's font assets
                    var fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
                    try
                    {
                        foreach (var (where, root) in roots)
                        {
                            foreach (var button in root.GetComponentsInChildren<Button>(true))
                            {
                                var labelTransform = button.transform.Find("Label");
                                if (labelTransform == null) continue;
                                var label = labelTransform.GetComponent<TMP_Text>();
                                var localized = labelTransform.GetComponent<LocalizedText>();
                                if (label == null || localized == null) continue;
                                var keys = new List<string>();
                                if (!string.IsNullOrEmpty(localized.Key)) keys.Add(localized.Key);
                                if (runtimeKeys.TryGetValue(button.name, out var more)) keys.AddRange(more);
                                runtimeTexts.TryGetValue(button.name, out var literals);
                                if (keys.Count == 0 && literals == null) continue;
                                var box = ((RectTransform)label.transform).rect;
                                var width = box.width - label.margin.x - label.margin.z;
                                var height = box.height - label.margin.y - label.margin.w;
                                if (!fonts.TryGetValue(label.font, out var font))
                                {
                                    var file = SourceFontOf(label.font);
                                    Assert.That(file, Is.Not.Null, $"{label.font.name}: no source font file");
                                    font = TMP_FontAsset.CreateFontAsset(file);
                                    fonts.Add(label.font, font);
                                }
                                probe.font = font;
                                probe.fontStyle = label.fontStyle;
                                probe.enableAutoSizing = false;
                                probe.fontSize = label.fontSizeMin;
                                probe.margin = Vector4.zero;
                                probe.textWrappingMode = TextWrappingModes.Normal;
                                // The probe measures (a 20-letter word at 30 is far wider than 100 units)
                                Assert.That(probe.GetPreferredValues("WWWWWWWWWWWWWWWWWWWW").x, Is.GreaterThan(100f), "probe");
                                void Measure(string text, string what)
                                {
                                    var size = probe.GetPreferredValues(text, width, 10000f);
                                    var widest = text.Split(' ').Max(w => probe.GetPreferredValues(w).x);
                                    if (size.y > height + 0.5f || widest > width + 0.5f)
                                        problems.Add($"{phone.x}x{phone.y} {where}: {PathOf(button.transform)} {what} \"{text}\" needs {Mathf.Max(widest, size.x):0}x{size.y:0}, has {width:0}x{height:0}");
                                }
                                foreach (var key in keys)
                                {
                                    foreach (var language in languages)
                                    {
                                        var text = tables[language].Get(key);
                                        if (text != key) Measure(text, $"'{key}' [{language}]");
                                    }
                                }
                                if (literals != null)
                                {
                                    foreach (var text in literals) Measure(text, "literal");
                                }
                            }
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(probe.gameObject);
                        foreach (var font in fonts.Values)
                        {
                            foreach (var atlas in font.atlasTextures) UnityEngine.Object.DestroyImmediate(atlas);
                            UnityEngine.Object.DestroyImmediate(font.material);
                            UnityEngine.Object.DestroyImmediate(font);
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty);
        }

        // A dynamic font's source file, or for a bundled static one the TTF beside it ("<Font> SDF.asset" next to
        // "<Font>.ttf", see FontBuilder: a static atlas keeps no link to its file)
        private static Font SourceFontOf(TMP_FontAsset asset)
        {
            var file = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(asset.creationSettings.sourceFontFileGUID));
            if (file != null) return file;
            var path = AssetDatabase.GetAssetPath(asset);
            const string suffix = " SDF.asset";
            return path.EndsWith(suffix, StringComparison.Ordinal)
                ? AssetDatabase.LoadAssetAtPath<Font>(path.Substring(0, path.Length - suffix.Length) + ".ttf")
                : null;
        }

        [Test]
        public void Button_labels_shrink_to_fit_their_button()
        {
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var label = button.transform.Find("Label");
                    if (label == null) continue;
                    var text = label.GetComponent<TMP_Text>();
                    if (text == null || !text.enableAutoSizing || text.fontSizeMin > text.fontSizeMax ||
                        text.textWrappingMode == TextWrappingModes.NoWrap || text.overflowMode == TextOverflowModes.Overflow)
                    {
                        problems.Add($"{where}: {PathOf(label)} does not shrink to fit");
                    }
                }
            });
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void Every_graphic_has_a_theme_role_and_every_camera_follows_the_background()
        {
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.GetComponent<ThemedGraphic>() == null) problems.Add($"{where}: {PathOf(graphic.transform)} has no ThemedGraphic");
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.GetComponent<ThemedCamera>() == null) problems.Add($"{where}: {PathOf(camera.transform)} has no ThemedCamera");
                }
            });
            Assert.That(problems, Is.Empty);
        }

        // Every button is the kit's (rounded, themed, pressed with a spring), or a kit box that presses like one (the
        // paywall's package rows); every toggle is a KitSwitch and every slider a KitSlider
        [Test]
        public void Every_button_is_a_kit_button()
        {
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var kitBox = button.GetComponent<KitElement>() != null && button.GetComponent<PressFeedback>() != null;
                    if (button.GetComponent<KitButton>() == null && !kitBox) problems.Add($"{where}: {PathOf(button.transform)} is not a kit button");
                }
                foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                {
                    if (toggle.GetComponent<KitSwitch>() == null) problems.Add($"{where}: {PathOf(toggle.transform)} is not a KitSwitch");
                }
                foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                {
                    if (slider.GetComponent<KitSlider>() == null) problems.Add($"{where}: {PathOf(slider.transform)} is not a KitSlider");
                }
            });
            Assert.That(problems, Is.Empty);
        }

        // The style picks every text's font from its role (Display, Title, Body, Caption)
        [Test]
        public void Every_text_has_a_text_role()
        {
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.GetComponent<ThemedText>() == null) problems.Add($"{where}: {PathOf(text.transform)} has no ThemedText");
                }
            });
            Assert.That(problems, Is.Empty);
        }

        // Scenes and prefabs are written with the default family's fonts: TMP's Liberation Sans (only a fallback of the
        // bundled fonts) is never a text's own font
        [Test]
        public void No_liberation_sans_in_scenes()
        {
            var problems = new List<string>();
            ForEachRoot((where, root) =>
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == null || text.font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0)
                        problems.Add($"{where}: {PathOf(text.transform)} uses {(text.font == null ? "no font" : text.font.name)}");
                }
            });
            Assert.That(problems, Is.Empty);
        }

        // Every view's panel opens with the kit's motion: a sheet (modal) or a screen transition (full screen), and a
        // full-screen one is filled with the style's background
        [Test]
        public void Every_panel_enters_with_a_kit_transition()
        {
            var problems = new List<string>();
            var panels = 0;
            ForEachRoot((where, root) =>
            {
                foreach (var view in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (view == null) continue;
                    var field = new SerializedObject(view).FindProperty("panel");
                    if (field == null || field.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var panel = field.objectReferenceValue as GameObject;
                    if (panel == null) continue;
                    panels++;
                    var transition = panel.GetComponent<ScreenTransition>();
                    if (panel.GetComponent<KitSheet>() == null && transition == null)
                        problems.Add($"{where}: {PathOf(panel.transform)} ({view.GetType().Name}) has no KitSheet or ScreenTransition");
                    if (transition != null && panel.GetComponent<KitBackground>() == null)
                        problems.Add($"{where}: {PathOf(panel.transform)} is a full-screen panel without KitBackground");
                }
            });
            Assert.That(panels, Is.GreaterThanOrEqualTo(4), "panels found");
            Assert.That(problems, Is.Empty);
        }

        // The menu: the style's background and its drifting shapes behind it, the title in the display font, a breathing
        // Play button, the settings gear, and the whole screen playing in when the scene starts
        [Test]
        public void The_menu_leads_with_a_breathing_play_button_under_a_display_title()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Additive);
            try
            {
                var canvas = scene.GetRootGameObjects().Single(r => r.name == "Canvas").transform;
                Assert.That(canvas.Find("Background").GetComponent<KitBackground>(), Is.Not.Null, "background");
                Assert.That(canvas.Find("Background").GetSiblingIndex(), Is.EqualTo(0), "background behind everything");
                var menu = canvas.Find("SafeArea/Menu");
                var entrance = menu.GetComponent<ScreenTransition>();
                Assert.That(entrance != null && entrance.EnterOnStart, Is.True, "menu enters on start");
                Assert.That(menu.Find("Title").GetComponent<ThemedText>().Role, Is.EqualTo(TextRole.Display), "title");
                var play = menu.Find("Play").GetComponent<KitButton>();
                Assert.That(play.Variant, Is.EqualTo(KitButtonVariant.Play), "play variant");
                Assert.That(play.GetComponent<IdlePulse>(), Is.Not.Null, "play breathes");
                // The settings gear in the top bar's right corner; About moved into the settings panel
                var gear = menu.Find("TopBar/Settings").GetComponent<KitButton>();
                Assert.That(gear.Variant, Is.EqualTo(KitButtonVariant.Icon), "gear variant");
                Assert.That(gear.Icon != null ? gear.Icon.sprite : null, Is.SameAs(KitSprites.Gear), "gear icon");
                Assert.That(menu.Find("About"), Is.Null, "no About button on the menu");
                var about = Load(SettingsPrefab).transform.Find("Panel/SafeArea/Card/About").GetComponent<KitButton>();
                Assert.That(about.Variant, Is.EqualTo(KitButtonVariant.Secondary), "settings About");
                Assert.That(about.Icon != null ? about.Icon.sprite : null, Is.SameAs(KitSprites.Info), "About icon");
                // Soft shapes over the background, behind the safe area's content, so a game without a logo still has a
                // dressed title screen
                var decor = canvas.Find("Decor");
                Assert.That(decor, Is.Not.Null, "decor");
                Assert.That(decor.GetSiblingIndex(), Is.LessThan(canvas.Find("SafeArea").GetSiblingIndex()), "decor behind the content");
                Assert.That(decor.GetComponentsInChildren<KitDecor>(true).Length, Is.GreaterThanOrEqualTo(6), "decor shapes");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
