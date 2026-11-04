using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Kturker.UnityBase.Tests.PlayMode
{
    // The menu re-arranges itself while it shows (MenuView's Awake, OnRectTransformDimensionsChange and Update, no test
    // calling Arrange): the real boot flow opens it, its canvas is turned into a world-space canvas whose size the test
    // sets (portrait, landscape, portrait), a feature adds a round button and games and the banner's room is kept in
    // landscape; it ends where it began, without a warning or an error (a layout loop logs one) on the way
    public sealed class MenuRotationTests
    {
        private static readonly Vector2 Portrait = new Vector2(1080f, 1920f);
        private static readonly Vector2 Landscape = new Vector2(1920f, 1080f);

        // Every log since the test began, to tell the boot's own messages from the arrangement's
        private readonly List<(LogType Type, string Message)> logs = new List<(LogType, string)>();

        [SetUp]
        public void Listen() => Application.logMessageReceived += Record;

        [TearDown]
        public void Stop()
        {
            Application.logMessageReceived -= Record;
            SafeArea.ReservedTop = 0f;
            logs.Clear();
        }

        private void Record(string message, string stack, LogType type) => logs.Add((type, message));

        [UnityTest]
        public IEnumerator The_menu_rearranges_itself_when_it_turns_and_turns_back() => UniTask.ToCoroutine(async () =>
        {
            await SceneManager.LoadSceneAsync("Boot").ToUniTask();
            var menu = await WaitFor<MenuView>(10f);
            Assert.That(menu, Is.Not.Null, "Menu did not open within 10 s");
            // Past the menu's entrance
            await UniTask.Delay(1000);
            var canvas = (RectTransform)menu.GetComponentInParent<Canvas>().rootCanvas.transform;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas.localScale = Vector3.one;

            await Resize(canvas, Portrait);
            var built = Snapshot(menu);
            Assert.That(menu.BottomBlock.anchorMin, Is.EqualTo(Vector2.zero), "the block as built");
            Assert.That(menu.BottomBlock.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(menu.BottomBlock.anchoredPosition.y, Is.EqualTo(MenuView.BottomInset).Within(0.01f));
            Assert.That(menu.ExtraSlot.IsChildOf(menu.BottomBlock), Is.True, "the features' rows in the block");
            // The boot's own plain logs and warnings are its business (BootFlowTests; an error still fails); from here on
            // only plain logs may come
            foreach (var (type, message) in logs.Where(l => l.Type == LogType.Log || l.Type == LogType.Warning).ToList()) Expect(type, message);
            logs.Clear();

            await Resize(canvas, Landscape);
            Assert.That(menu.BottomBlock.anchorMin.x, Is.EqualTo(0.5f), "the block in the right half");
            var column = menu.transform.Find("Column");
            Assert.That(column != null && column.gameObject.activeSelf, Is.True, "the landscape column");
            Assert.That(menu.ExtraSlot.parent, Is.SameAs(column), "the features' rows in the column");
            Assert.That(Games(menu).parent, Is.SameAs(column), "the further games in the column");
            Clear(menu, "turned");

            // A feature's round button and new games while turned: the block and the column follow
            var side = SideWithRoom(menu);
            menu.AddSideButton(side, null, "menu.play", null);
            foreach (Transform copy in Games(menu)) copy.gameObject.SetActive(false);
            var binder = Object.FindFirstObjectByType<MenuScope>().Container.Resolve<UiBinder>();
            menu.ShowGames(new[] { "menu.play", "menu.play", "menu.play" }, binder);
            await UniTask.DelayFrame(2);
            Assert.That(Games(menu).Cast<Transform>().Count(t => t.gameObject.activeSelf), Is.EqualTo(2), "two further games shown");
            Clear(menu, "with a new button and games");

            // The banner's room kept: the top bar comes down, the menu re-arranges under it
            SafeArea.ReservedTop = 150f * Screen.height / 1080f;
            await UniTask.DelayFrame(2);
            Assert.That(menu.TopBar.anchoredPosition.y, Is.LessThan(-MenuView.TopInset), "the top bar moved down for the banner");
            Clear(menu, "with the banner");

            SafeArea.ReservedTop = 0f;
            await Resize(canvas, Portrait);
            Assert.That(Snapshot(menu), Is.EqualTo(built), "back to portrait: as built");
            Assert.That(column.gameObject.activeSelf, Is.False, "the landscape column hidden");

            // Plain logs, and TMP's note that a font has no ellipsis (a text re-fitted at a new size), are no layout trouble
            foreach (var (type, message) in logs.Where(l => l.Type == LogType.Log ||
                         l.Type == LogType.Warning && l.Message.StartsWith(NoEllipsis))) Expect(type, message);
            LogAssert.NoUnexpectedReceived();
        });

        private const string NoEllipsis = "The character used for Ellipsis is not available in font asset";

        private static void Expect(LogType type, string message) => LogAssert.Expect(type, new Regex("^" + Regex.Escape(message) + "$"));

        private static async UniTask Resize(RectTransform canvas, Vector2 size)
        {
            canvas.sizeDelta = size;
            await UniTask.DelayFrame(2);
        }

        // The further games' row, wherever it is
        private static Transform Games(MenuView menu) => menu.GetComponentsInChildren<Transform>(true).First(t => t.name == "Games");

        private static MenuSide SideWithRoom(MenuView menu)
        {
            var lefts = menu.SideRow.Cast<Transform>().Count(t => t.name.StartsWith("Left"));
            return lefts < MenuView.SideSlots ? MenuSide.Left : MenuSide.Right;
        }

        // The bottom block and the column clear of the top bar, the shown title or logo and each other, inside the view
        private static void Clear(MenuView menu, string when)
        {
            var view = World((RectTransform)menu.transform);
            var bar = World(menu.TopBar);
            var block = World(menu.BottomBlock);
            var column = World((RectTransform)menu.transform.Find("Column"));
            var bands = new[] { menu.Title.rectTransform, menu.TitleLogo.rectTransform }.Where(b => b.gameObject.activeInHierarchy).ToList();
            Assert.That(bands, Is.Not.Empty, "a title or a logo");
            foreach (var (name, part) in new[] { ("block", block), ("column", column) })
            {
                Assert.That(Overlap(part, bar), Is.False, $"{when}: the {name} {part} overlaps the top bar {bar}");
                foreach (var band in bands)
                    Assert.That(Overlap(part, World(band)), Is.False, $"{when}: the {name} {part} overlaps the {band.name} {World(band)}");
                Assert.That(part.xMin >= view.xMin - 0.5f && part.xMax <= view.xMax + 0.5f && part.yMin >= view.yMin - 0.5f, Is.True,
                    $"{when}: the {name} {part} outside the view {view}");
            }
            foreach (var band in bands)
                Assert.That(Overlap(World(band), bar), Is.False, $"{when}: the {band.name} overlaps the top bar");
            Assert.That(Overlap(block, column), Is.False, $"{when}: the block {block} overlaps the column {column}");
        }

        private static bool Overlap(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.5f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.5f;

        private static Rect World(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // The placements Arrange writes (the block's height is its content's, which the test changed; the features' rows'
        // and the further games' anchors, positions and sizes are their layout group's, which takes the games' row in as
        // soon as the test shows it in a one-game build: for them their parent, place and scale)
        private static List<string> Snapshot(MenuView menu)
        {
            var parts = new[]
            {
                menu.Title.rectTransform, menu.TitleLogo.rectTransform, menu.TitleStroke, menu.Version.rectTransform, menu.BottomBlock,
                (RectTransform)menu.ExtraSlot, (RectTransform)Games(menu)
            };
            return parts.Select(r =>
            {
                var size = r == menu.BottomBlock ? new Vector2(r.sizeDelta.x, 0f) : r.sizeDelta;
                var laidOut = r == menu.ExtraSlot || r == Games(menu);
                var position = r == menu.BottomBlock ? Vector2.zero : r.anchoredPosition;
                var place = laidOut ? "laid out" : $"{r.anchorMin:F3} {r.anchorMax:F3} {r.pivot:F3} {position:F2} {size:F2}";
                return $"{r.name}: {place} {r.localScale:F3} in {r.parent.name} at {r.GetSiblingIndex()}";
            }).ToList();
        }

        private static async UniTask<T> WaitFor<T>(float seconds) where T : Object
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                var found = Object.FindFirstObjectByType<T>();
                if (found != null) return found;
                await UniTask.Yield();
            }
            return null;
        }
    }
}
