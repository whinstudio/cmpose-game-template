using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // Screens laid out the way a device lays them out, without a device: every canvas becomes a world-space canvas the
    // size its CanvasScaler (match 0.5) gives the screen, every child shown (rows the presenter shows count), safe areas
    // as SafeArea anchors them (ReservedTop and ReservedBottom included), bound to a theme the way UiBinder binds it,
    // layouts rebuilt twice, fit scales and touch areas fitted, the canvas update run (framed cards' title bands) and every text's mesh updated, all of it twice: a text
    // group (KitTextGroup) refits when its members' meshes update, and a device draws them again at the new size before
    // the frame ends. Rects read in canvas units.
    internal static class LayoutStage
    {
        // Portrait phones (the 1080 x 1920 reference, a 20:9 phone), a tablet, and the same turned to landscape
        public static readonly (string Name, Vector2 Screen)[] Screens =
        {
            ("16:9", new Vector2(1080f, 1920f)),
            ("20:9", new Vector2(1080f, 2400f)),
            ("tablet", new Vector2(1536f, 2048f)),
            ("16:9 landscape", new Vector2(1920f, 1080f)),
            ("20:9 landscape", new Vector2(2400f, 1080f)),
            ("tablet landscape", new Vector2(2048f, 1536f))
        };

        // The families' tallest font pair (.cmpose/themes.json: bright's default, pastel's second): Baloo 2 display, its
        // line box 1.6 em where the kit's sizes count on 1.2 to 1.36 (ThemedText.IsTall), over Nunito body
        public const string TallDisplay = "Assets/_Project/Core/Presentation/Fonts/Baloo2/Baloo2-Bold SDF.asset";
        public const string TallBody = "Assets/_Project/Core/Presentation/Fonts/Nunito/Nunito-Regular SDF.asset";
        // The family TallStyle draws in: bright ships the tall pair, and it is chunky
        public const string TallFamily = "bright";

        // The app's style with the tall pair in place of its fonts and bright's family id (the family that ships the
        // pair): a game may pick it, so the layout rules run in it too, and in the chunky look (KitShape.Chunky: framed
        // modal cards, raised pill buttons) that only a family other than clean draws; made collects it for the
        // caller's TearDown
        public static ThemeStyle TallStyle(List<Object> made)
        {
            var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TallDisplay);
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TallBody);
            Assert.That(display, Is.Not.Null, TallDisplay);
            Assert.That(body, Is.Not.Null, TallBody);
            var style = Object.Instantiate(ThemeStyle.Load());
            made.Add(style);
            var serialized = new SerializedObject(style);
            serialized.FindProperty("displayFont").objectReferenceValue = display;
            serialized.FindProperty("bodyFont").objectReferenceValue = body;
            serialized.FindProperty("familyId").stringValue = TallFamily;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return style;
        }

        // A copy of a prefab laid out on screen; made collects it for the caller's TearDown
        public static GameObject Prefab(string path, Vector2 screen, IThemeSource theme, List<Object> made)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var copy = Object.Instantiate(prefab);
            made.Add(copy);
            Lay(copy, screen, theme);
            return copy;
        }

        // A scene opened next to the open ones and laid out on screen; its root "Canvas" is returned. The caller closes
        // the scene (EditorSceneManager.CloseScene(scene, true)), never saving it
        public static Transform Scene(string path, Vector2 screen, IThemeSource theme, out Scene scene)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Transform canvas = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() == null) continue;
                Lay(root, screen, theme);
                if (root.name == "Canvas") canvas = root.transform;
            }
            Assert.That(canvas, Is.Not.Null, $"{path}: Canvas");
            return canvas;
        }

        // Lays a staged root out again after a test changed it (children added, shown or hidden)
        public static void Layout(GameObject root)
        {
            for (var frame = 0; frame < 2; frame++)
            {
                for (var pass = 0; pass < 2; pass++)
                {
                    foreach (var group in root.GetComponentsInChildren<LayoutGroup>())
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
                    foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>())
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)fitter.transform);
                }
                foreach (var fit in root.GetComponentsInChildren<KitFitScale>()) fit.Fit();
                // The canvas update a device runs each frame: what registered for it (a framed card's title band, placed
                // after layout) runs on the rects laid out above
                Canvas.ForceUpdateCanvases();
                foreach (var area in root.GetComponentsInChildren<KitTouchArea>()) area.Fit();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
            }
        }

        // Per staged root, the objects it was built hidden (IndexPath): Lay shows them all
        private static readonly Dictionary<GameObject, HashSet<string>> builtHidden = new Dictionary<GameObject, HashSet<string>>();

        // The sibling-index path of t under its root
        public static string IndexPath(Transform t) => t.parent == null ? "" : $"{IndexPath(t.parent)}/{t.GetSiblingIndex()}";

        // Shown as built: nothing from t up to stop (excluded) was built hidden in its staged root
        public static bool ShownAsBuilt(Transform t, Transform stop)
        {
            if (!builtHidden.TryGetValue(t.root.gameObject, out var hidden)) return true;
            for (; t != null && t != stop; t = t.parent)
                if (hidden.Contains(IndexPath(t))) return false;
            return true;
        }

        // Lays a staged root's safe areas out again for the current SafeArea.ReservedTop and ReservedBottom
        public static void Reserve(GameObject root, Vector2 screen)
        {
            SafeAreas(root, screen);
            Layout(root);
        }

        // Lays a root that is open already (a scene a test opened) out on screen, the way Scene and Prefab lay theirs
        public static void Lay(GameObject root, Vector2 screen, IThemeSource theme)
        {
            foreach (var gone in builtHidden.Keys.Where(k => k == null).ToList()) builtHidden.Remove(gone);
            builtHidden[root] = new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Where(t => !t.gameObject.activeSelf)
                .Select(IndexPath));
            var scaler = root.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, $"{root.name}: CanvasScaler");
            var reference = scaler.referenceResolution;
            var scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(screen.x / reference.x, 2f), Mathf.Log(screen.y / reference.y, 2f),
                scaler.matchWidthOrHeight));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)root.transform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = screen / scale;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            SafeAreas(root, screen);
            foreach (var element in root.GetComponentsInChildren<KitElement>()) element.Bind(theme);
            foreach (var graphic in root.GetComponentsInChildren<ThemedGraphic>()) graphic.Bind(theme);
            foreach (var text in root.GetComponentsInChildren<ThemedText>()) text.Bind(theme);
            Layout(root);
        }

        private static void SafeAreas(GameObject root, Vector2 screen)
        {
            var pixels = new Vector2Int(Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y));
            var (min, max) = SafeArea.Anchors(new Rect(Vector2.zero, screen), pixels, SafeArea.ReservedBottom, SafeArea.ReservedTop);
            foreach (var safe in root.GetComponentsInChildren<SafeArea>())
            {
                var area = (RectTransform)safe.transform;
                area.anchorMin = min;
                area.anchorMax = max;
                area.offsetMin = Vector2.zero;
                area.offsetMax = Vector2.zero;
            }
        }

        // A rect in world (canvas) units
        public static Rect World(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // The gap between two rects (0 when they touch or overlap)
        public static float Distance(Rect a, Rect b)
        {
            var dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            var dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static string PathOf(Transform t) => t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";
    }
}
