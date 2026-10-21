using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The end cards opened settled (an automation run's store shots): the final stars, reward and score at once
    public sealed class ShellCardTests
    {
        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        [Test]
        public void Settled_level_complete_shows_final_stars_and_reward()
        {
            var card = LevelComplete(out var stars, out var reward);

            card.Show(1, 2, 60, true);

            Assert.That(card.IsShown, Is.True);
            Assert.That(stars.Landed, Is.EqualTo(2));
            Assert.That(stars.Playing, Is.False, "no star still to land");
            Assert.That(reward.Value, Is.EqualTo(60), "the reward at once");
            Assert.That(reward.Counting, Is.False);
        }

        [Test]
        public void Settled_level_complete_with_zero_stars_shows_the_reward()
        {
            var card = LevelComplete(out var stars, out var reward);

            card.Show(1, 0, 25, true);

            Assert.That(stars.Landed, Is.Zero);
            Assert.That(stars.Playing, Is.False);
            Assert.That(reward.Value, Is.EqualTo(25));
        }

        [Test]
        public void Unsettled_level_complete_starts_from_nothing()
        {
            var card = LevelComplete(out var stars, out var reward);

            card.Show(1, 2, 60, false);

            Assert.That(stars.Playing, Is.True, "the stars land one by one");
            Assert.That(stars.Landed, Is.Zero);
            Assert.That(reward.Value, Is.Zero, "the reward counts up after the stars");
        }

        [Test]
        public void Settled_game_over_shows_the_final_score()
        {
            var card = GameOver(out var score);

            card.Show(42, 128, true);

            Assert.That(card.IsShown, Is.True);
            Assert.That(score.Value, Is.EqualTo(42));
            Assert.That(score.Counting, Is.False);

            card.Show(42, 128, false);
            Assert.That(score.Counting, Is.True, "a player sees it count up");
        }

        private LevelCompleteCard LevelComplete(out StarRow stars, out CountUpText reward)
        {
            var root = Ui("LevelComplete", canvas.transform);
            var card = root.AddComponent<LevelCompleteCard>();
            var panel = Ui("Panel", root.transform);
            panel.SetActive(false);
            stars = Ui("Stars", panel.transform).AddComponent<StarRow>();
            var slots = new Image[3];
            var fills = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                slots[i] = Ui($"Slot{i}", stars.transform).AddComponent<Image>();
                fills[i] = Ui($"Fill{i}", stars.transform).AddComponent<Image>();
            }
            var row = new SerializedObject(stars);
            Array(row.FindProperty("slots"), slots);
            Array(row.FindProperty("fills"), fills);
            row.ApplyModifiedPropertiesWithoutUndo();
            reward = Text("Reward", panel.transform).gameObject.AddComponent<CountUpText>();
            reward.SetFormat("+{0:0}");
            var wiring = Card(card, panel);
            wiring.FindProperty("title").objectReferenceValue = Text("Title", panel.transform).gameObject.AddComponent<LocalizedText>();
            wiring.FindProperty("stars").objectReferenceValue = stars;
            wiring.FindProperty("reward").objectReferenceValue = reward;
            wiring.FindProperty("celebration").objectReferenceValue = Ui("Celebration", panel.transform).AddComponent<Celebration>();
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private GameOverCard GameOver(out CountUpText score)
        {
            var root = Ui("ShellGameOver", canvas.transform);
            var card = root.AddComponent<GameOverCard>();
            var panel = Ui("Panel", root.transform);
            panel.SetActive(false);
            score = Text("Score", panel.transform).gameObject.AddComponent<CountUpText>();
            var wiring = Card(card, panel);
            wiring.FindProperty("score").objectReferenceValue = score;
            wiring.FindProperty("best").objectReferenceValue = Text("Best", panel.transform).gameObject.AddComponent<LocalizedText>();
            wiring.FindProperty("newBest").objectReferenceValue = Ui("NewBest", panel.transform).AddComponent<KitBadge>();
            wiring.FindProperty("celebration").objectReferenceValue = Ui("Celebration", panel.transform).AddComponent<Celebration>();
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // ShellCardView's own parts: the panel (no sheet: shown by SetActive) and its two buttons
        private SerializedObject Card(ShellCardView card, GameObject panel)
        {
            var wiring = new SerializedObject(card);
            wiring.FindProperty("panel").objectReferenceValue = panel;
            wiring.FindProperty("primaryButton").objectReferenceValue = Ui("Primary", panel.transform).AddComponent<Button>();
            wiring.FindProperty("menuButton").objectReferenceValue = Ui("Menu", panel.transform).AddComponent<Button>();
            return wiring;
        }

        private static void Array(SerializedProperty property, Image[] images)
        {
            property.arraySize = images.Length;
            for (var i = 0; i < images.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        }

        private static TMP_Text Text(string name, Transform parent) => Ui(name, parent).AddComponent<TextMeshProUGUI>();

        private static GameObject Ui(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
