using System.Collections.Generic;
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
    // layouts rebuilt twice, fit scales and touch areas fitted and every text's mesh updated. Rects read in canvas units.
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
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var group in root.GetComponentsInChildren<LayoutGroup>())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
                foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)fitter.transform);
            }
            foreach (var fit in root.GetComponentsInChildren<KitFitScale>()) fit.Fit();
            foreach (var area in root.GetComponentsInChildren<KitTouchArea>()) area.Fit();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
        }

        private static void Lay(GameObject root, Vector2 screen, IThemeSource theme)
        {
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
            foreach (var element in root.GetComponentsInChildren<KitElement>()) element.Bind(theme);
            foreach (var graphic in root.GetComponentsInChildren<ThemedGraphic>()) graphic.Bind(theme);
            foreach (var text in root.GetComponentsInChildren<ThemedText>()) text.Bind(theme);
            Layout(root);
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
