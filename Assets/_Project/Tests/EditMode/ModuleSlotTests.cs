using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Sample.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class ModuleSlotTests
    {
        private const float Visible = 1500f;

        private GameObject root;
        private ModuleSlot slot;
        private RectTransform content;

        [SetUp]
        public void SetUp()
        {
            // The Game scene's shape: Modules (stretched) > Viewport (stretched) > Content (top, grows with the panels)
            root = new GameObject("Modules", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(1000f, Visible);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            slot = root.AddComponent<ModuleSlot>();
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("content").objectReferenceValue = content;
            serialized.FindProperty("viewport").objectReferenceValue = viewport;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private RectTransform AddPanel(string name)
        {
            var panel = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(slot.Content, false);
            return panel;
        }

        private void Layout()
        {
            slot.Fit();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        [Test]
        public void The_sample_view_is_a_resources_prefab()
        {
            Assert.That(Resources.Load<SampleView>(SampleView.ResourcePath), Is.Not.Null);
        }

        [Test]
        public void Spawn_puts_the_panel_under_the_slot()
        {
            var binder = new UiBinder(new TableLocalizer(new FakeStringSource(), new FakeLog(), false),
                new FixedThemeSource(Resources.Load<Palette>("Theme/Light"), Resources.Load<ThemeStyle>(ThemeStyle.ResourcePath)));
            var view = SampleView.Spawn(slot.Content, binder);
            try
            {
                Assert.That(view.transform.parent, Is.EqualTo(slot.Content));
            }
            finally
            {
                // A panel that missed the slot is not under root, so TearDown would leave it in the scene
                if (view != null) Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Scene_content_is_the_slots_content()
        {
            Assert.That(ModuleSlot.SceneContent(), Is.EqualTo(slot.Content));
        }

        [Test]
        public void Scene_content_without_a_slot_names_the_missing_module_slot()
        {
            Object.DestroyImmediate(root);
            var error = Assert.Throws<System.InvalidOperationException>(() => ModuleSlot.SceneContent());
            Assert.That(error.Message, Does.Contain("ModuleSlot").And.Contain("Game scene"));
        }

        [Test]
        public void One_panel_fills_the_visible_area_and_nothing_scrolls()
        {
            var panel = AddPanel("A");
            Layout();
            Assert.That(panel.rect.height, Is.EqualTo(Visible).Within(0.01f));
            Assert.That(content.rect.height, Is.EqualTo(Visible).Within(0.01f));
        }

        [Test]
        public void Three_panels_each_take_the_visible_height_and_stack_into_a_scroll()
        {
            var panels = new[] { AddPanel("A"), AddPanel("B"), AddPanel("C") };
            Layout();
            foreach (var panel in panels) Assert.That(panel.rect.height, Is.EqualTo(Visible).Within(0.01f), panel.name);
            Assert.That(content.rect.height, Is.EqualTo(3 * Visible).Within(0.01f));
        }

        [Test]
        public void A_new_visible_height_refits_the_panels()
        {
            var panels = new[] { AddPanel("A"), AddPanel("B") };
            Layout();
            ((RectTransform)root.transform).sizeDelta = new Vector2(1600f, 900f);
            Layout();
            foreach (var panel in panels) Assert.That(panel.rect.height, Is.EqualTo(900f).Within(0.01f), panel.name);
            Assert.That(content.rect.height, Is.EqualTo(2 * 900f).Within(0.01f));
        }

        [Test]
        public void Without_a_wired_viewport_the_content_parent_is_the_visible_area()
        {
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("viewport").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var panels = new[] { AddPanel("A"), AddPanel("B") };
            Layout();
            foreach (var panel in panels) Assert.That(panel.rect.height, Is.EqualTo(Visible).Within(0.01f), panel.name);
        }

        [Test]
        public void An_unwired_slot_does_nothing()
        {
            var go = new GameObject("Unwired", typeof(RectTransform));
            try
            {
                Assert.DoesNotThrow(() => go.AddComponent<ModuleSlot>().Fit());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void The_game_scene_slot_scrolls_vertically_inside_a_masked_viewport()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Additive);
            try
            {
                ModuleSlot found = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    found = go.GetComponentInChildren<ModuleSlot>(true);
                    if (found != null) break;
                }
                Assert.That(found, Is.Not.Null);
                var scroll = found.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(scroll.content, Is.EqualTo(found.Content));
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(found.Content.parent, Is.EqualTo(scroll.viewport));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
