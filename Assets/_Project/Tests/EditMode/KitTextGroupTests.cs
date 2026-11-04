using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    // A group of single-line texts shares one size: the largest at which every shown member fits its box less the side
    // padding, down to min and, reported, to floor; fitted again when a member's text or font changes.
    public sealed class KitTextGroupTests
    {
        private const float Max = 72f;
        private const float Min = 34f;
        private const float Floor = 18f;
        private const string ShortText = "Music";
        private const string LongText = "Notifications and vibration";

        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;
        private TMP_FontAsset nunito;
        private TMP_Text probe;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, 1920f);
            nunito = Font("Nunito/Nunito-Bold SDF");
            probe = Label("Probe", 2000f, string.Empty);
            probe.textWrappingMode = TextWrappingModes.NoWrap;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private static TMP_FontAsset Font(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/_Project/Core/Presentation/Fonts/{path}.asset");
            Assert.That(font, Is.Not.Null, path);
            return font;
        }

        // A label width units wide (100 high) in Nunito holding text
        private TMP_Text Label(string name, float width, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 100f);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = nunito;
            label.text = text;
            return label;
        }

        // The one-line width of text at size in font (Nunito unless given)
        private float Width(string text, float size, TMP_FontAsset font = null)
        {
            probe.font = font != null ? font : nunito;
            probe.fontSize = size;
            return probe.GetPreferredValues(text).x;
        }

        private KitTextGroup Group(float sidePadding, params TMP_Text[] members)
        {
            var go = new GameObject("Group", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var group = go.AddComponent<KitTextGroup>();
            group.Configure(Max, Min, Floor, sidePadding, members);
            return group;
        }

        [Test]
        public void All_members_share_the_largest_size_at_which_each_fits()
        {
            var width = Width(LongText, 60f) + 1f;
            Assert.That(Width(LongText, 61f), Is.GreaterThan(width), "61 does not fit");
            var shortLabel = Label("Short", width, ShortText);
            var longLabel = Label("Long", width, LongText);

            var group = Group(0f, shortLabel, longLabel);

            Assert.That(group.Size, Is.EqualTo(60f));
            Assert.That(group.Fits, Is.True);
            Assert.That(shortLabel.fontSize, Is.EqualTo(group.Size));
            Assert.That(longLabel.fontSize, Is.EqualTo(group.Size));
            foreach (var label in new[] { shortLabel, longLabel })
            {
                Assert.That(label.fontSize, Is.LessThanOrEqualTo(Max), label.name);
                Assert.That(label.enableAutoSizing, Is.False, label.name);
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap), label.name);
                Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Overflow), label.name);
            }
        }

        [Test]
        public void Short_texts_stay_at_max()
        {
            var group = Group(0f, Label("A", 900f, ShortText), Label("B", 900f, "Sfx"));

            Assert.That(group.Size, Is.EqualTo(Max));
            Assert.That(group.Fits, Is.True);
        }

        [Test]
        public void A_text_too_long_for_min_goes_down_to_the_floor_and_reports_it()
        {
            var label = Label("Long", Width(LongText, 25f) + 1f, LongText);

            var group = Group(0f, label);

            Assert.That(group.Fits, Is.False);
            Assert.That(group.Size, Is.LessThan(Min));
            Assert.That(group.Size, Is.GreaterThanOrEqualTo(Floor));
            Assert.That(group.Size, Is.EqualTo(25f));
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.lineCount, Is.EqualTo(1));

            // Too long even at floor: it stops there, still on one line
            var narrow = Label("Narrow", Width(LongText, 10f), LongText);
            var stuck = Group(0f, narrow);
            Assert.That(stuck.Size, Is.EqualTo(Floor));
            Assert.That(stuck.Fits, Is.False);
            narrow.ForceMeshUpdate();
            Assert.That(narrow.textInfo.lineCount, Is.EqualTo(1));
        }

        [Test]
        public void Side_padding_counts()
        {
            var width = Width(LongText, Max) + 10f;
            var label = Label("Long", width, LongText);

            var padded = Group(0f, label);
            Assert.That(padded.Size, Is.EqualTo(Max), "fits without padding");
            padded.Configure(Max, Min, Floor, 32f, label);

            Assert.That(padded.Size, Is.LessThan(Max));
            Assert.That(Width(LongText, padded.Size), Is.LessThanOrEqualTo(width - 64f + 0.01f));
            Assert.That(Width(LongText, padded.Size + 1f), Is.GreaterThan(width - 64f));
        }

        [Test]
        public void Refits_when_a_member_text_changes()
        {
            var width = Width(LongText, 50f) + 1f;
            var label = Label("Label", width, ShortText);
            var group = Group(0f, label, Label("Other", width, "Sfx"));
            Assert.That(group.Size, Is.EqualTo(Max));

            // TMP reports the change when it regenerates the text (a canvas update; here at once)
            label.text = LongText;
            label.ForceMeshUpdate();
            Assert.That(group.Size, Is.EqualTo(50f), "longer");
            Assert.That(label.fontSize, Is.EqualTo(50f));

            label.text = ShortText;
            label.ForceMeshUpdate();
            Assert.That(group.Size, Is.EqualTo(Max), "shorter");
            Assert.That(label.fontSize, Is.EqualTo(Max));
        }

        [Test]
        public void Hidden_members_do_not_count()
        {
            var width = Width(LongText, 50f) + 1f;
            var shown = Label("Shown", width, ShortText);
            var hidden = Label("Hidden", width, LongText);
            hidden.gameObject.SetActive(false);

            var group = Group(0f, shown, hidden);

            Assert.That(group.Size, Is.EqualTo(Max));
            Assert.That(hidden.fontSize, Is.EqualTo(Max), "a hidden member takes the shared size too");
            hidden.gameObject.SetActive(true);
            group.Fit();
            Assert.That(group.Size, Is.EqualTo(50f), "shown again");
        }

        // Hiding a member regenerates no text, so the group looks again before the canvases draw
        [Test]
        public void Hiding_a_member_refits_before_the_next_draw()
        {
            var width = Width(LongText, 50f) + 1f;
            var shown = Label("Shown", width, ShortText);
            var other = Label("Other", width, LongText);
            var group = Group(0f, shown, other);
            Assert.That(group.Size, Is.EqualTo(50f));

            other.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();

            Assert.That(group.Size, Is.EqualTo(Max), "hidden: the short text alone");
            Assert.That(shown.fontSize, Is.EqualTo(Max));
        }

        // A row of boxes as wide as their texts (a footer's links): width units wide, its members spacing apart
        private (KitTextGroup Group, TMP_Text First, TMP_Text Second) Row(float width, float spacing, string first, string second)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 100f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var a = Label("First", 10f, first);
            var b = Label("Second", 10f, second);
            a.transform.SetParent(go.transform, false);
            b.transform.SetParent(go.transform, false);
            var group = go.AddComponent<KitTextGroup>();
            group.ConfigureRow(Max, Min, Floor, a, b);
            return (group, a, b);
        }

        // Each box takes its text's width, so the row fits when the texts side by side do, and the boxes follow a refit:
        // none is left squeezed under its text
        [Test]
        public void A_row_fits_its_texts_side_by_side_and_its_boxes_follow()
        {
            const float spacing = 24f;
            var width = Width(ShortText, 60f) + spacing + Width("Sfx", 60f) + 1f;
            Assert.That(Width(ShortText, 61f) + spacing + Width("Sfx", 61f), Is.GreaterThan(width), "61 does not fit");

            var (group, first, second) = Row(width, spacing, ShortText, "Sfx");

            Assert.That(group.Size, Is.EqualTo(60f));
            Assert.That(group.Fits, Is.True);
            foreach (var label in new[] { first, second })
                Assert.That(label.rectTransform.rect.width, Is.EqualTo(Width(label.text, 60f)).Within(0.01f), label.name);

            first.text = LongText;
            first.ForceMeshUpdate();

            Assert.That(group.Size, Is.LessThan(60f));
            Assert.That(Width(LongText, group.Size) + spacing + Width("Sfx", group.Size), Is.LessThanOrEqualTo(width + 0.01f));
            if (group.Size > Floor)
                Assert.That(Width(LongText, group.Size + 1f) + spacing + Width("Sfx", group.Size + 1f), Is.GreaterThan(width));
            foreach (var label in new[] { first, second })
                Assert.That(label.rectTransform.rect.width, Is.EqualTo(Width(label.text, group.Size)).Within(0.01f), label.name);
        }

        // A row sharing its line with a centred text (the settings card's footer in the title's row): what comes before
        // its flexible child ends left of the text and what comes after starts right of it, spacing apart
        [Test]
        public void A_row_keeps_clear_of_a_centred_text()
        {
            const float spacing = 24f;
            // Beside the title's letters (spacing clear) each side has room for the short text at 50 and no more
            var half = Width(LongText, 40f) / 2f + spacing;
            var width = 2f * (half + Width(ShortText, 50f) + 0.5f);
            Assert.That(Width(ShortText, 51f), Is.GreaterThan(width / 2f - half), "51 does not keep clear");
            Assert.That(Width(ShortText, Max) + spacing + Width("Sfx", Max), Is.LessThanOrEqualTo(width), "the row alone fits at max");
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 100f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var first = Label("First", 10f, ShortText);
            first.transform.SetParent(go.transform, false);
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(go.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var second = Label("Second", 10f, "Sfx");
            second.transform.SetParent(go.transform, false);
            var group = go.AddComponent<KitTextGroup>();
            group.ConfigureRow(Max, Min, Floor, first, second);
            Assert.That(group.Size, Is.EqualTo(Max), "alone");
            var title = Label("Title", width, LongText);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.fontSize = 40f;

            group.KeepClearOf(title);

            Assert.That(group.Size, Is.EqualTo(50f), "the largest that keeps clear");
            var centre = title.rectTransform.position.x;
            Assert.That(LayoutStage.World(first.rectTransform).xMax, Is.LessThanOrEqualTo(centre - half + 0.5f));
            Assert.That(LayoutStage.World(second.rectTransform).xMin, Is.GreaterThanOrEqualTo(centre + half - 0.5f));

            group.KeepClearOf(null);
            Assert.That(group.Size, Is.EqualTo(Max), "free again");
        }

        // A message's whole words wrap onto two lines at most inside its box, at the largest size that allows it
        [Test]
        public void A_lines_group_wraps_onto_two_lines_at_most()
        {
            var label = Label("Message", Width(LongText, 50f) * 0.6f, LongText);
            ((RectTransform)label.transform).sizeDelta = new Vector2(Width(LongText, 50f) * 0.6f, 400f);
            var group = label.gameObject.AddComponent<KitTextGroup>();

            group.ConfigureLines(Max, Min, Floor, 2, label);

            Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            Assert.That(label.enableAutoSizing, Is.False);
            Assert.That(group.Fits, Is.True);
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.lineCount, Is.EqualTo(2), "two lines at the largest size, not three at a larger one");
            label.fontSize = group.Size + 1f;
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.lineCount, Is.GreaterThan(2), "one size more takes a third line");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => group.ConfigureLines(Max, Min, Floor, 1, label));
        }

        [Test]
        public void A_row_needs_a_horizontal_layout_and_its_own_children()
        {
            var go = new GameObject("NoLayout", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var bare = go.AddComponent<KitTextGroup>();
            var label = Label("Label", 100f, ShortText);
            Assert.Throws<System.InvalidOperationException>(() => bare.ConfigureRow(Max, Min, Floor, label));

            go.AddComponent<HorizontalLayoutGroup>();
            Assert.Throws<System.ArgumentException>(() => bare.ConfigureRow(Max, Min, Floor, label), "not a child of the row");
        }

        // A button turns its label bold (KitButton.LabelFont) when it is bound, after its group fitted the regular label:
        // the group fits the bold label again, though the label's role and font stay as they were
        [Test]
        public void A_member_turned_bold_refits_the_group()
        {
            const float sidePadding = 32f;
            var style = ThemeStyle.Load();
            var body = style.BodyFont;
            Assert.That(body, Is.Not.Null, "the app's body font");
            var button = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            var label = button.Label;
            label.GetComponent<ThemedText>().SetRole(TextRole.Body);
            label.font = body;
            label.fontStyle = FontStyles.Normal;
            // The group measures the text alone (no margins), as the probe does
            label.margin = Vector4.zero;
            label.text = LongText;
            ((RectTransform)button.transform).sizeDelta = new Vector2(Width(LongText, 44f, body) + 2f * sidePadding + 1f, 120f);
            var group = Group(sidePadding, label);
            group.Fit();
            Assert.That(group.Size, Is.EqualTo(44f), "fitted regular");

            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            button.Bind(new FakeThemeSource { Current = palette, Style = style });

            Assert.That(label.fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Bold));
            Assert.That(label.font, Is.SameAs(body), "same font");
            Assert.That(label.GetPreferredValues(label.text).x, Is.LessThanOrEqualTo(label.rectTransform.rect.width - 2f * sidePadding + 0.01f),
                $"bold at {label.fontSize}");
        }

        // A member's ThemedText takes the font and leaves the size to the group, which fits again for the new font (a
        // tall font would otherwise turn autosizing on)
        [Test]
        public void A_member_font_change_refits_the_group_and_does_not_autosize()
        {
            var baloo = Font("Baloo2/Baloo2-Bold SDF");
            Assert.That(ThemedText.IsTall(baloo), Is.True);
            var width = Width(LongText, 50f) + 1f;
            var label = Label("Label", width, LongText);
            var themed = label.gameObject.AddComponent<ThemedText>();
            themed.SetRole(TextRole.Display);
            var group = Group(0f, label);
            Assert.That(themed.GroupOwned, Is.True);
            Assert.That(group.Size, Is.EqualTo(50f));

            var style = Object.Instantiate(ThemeStyle.Load());
            made.Add(style);
            var serialized = new SerializedObject(style);
            serialized.FindProperty("displayFont").objectReferenceValue = baloo;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            themed.Bind(new FakeThemeSource { Style = style });

            Assert.That(label.font, Is.SameAs(baloo));
            Assert.That(label.enableAutoSizing, Is.False);
            Assert.That(label.fontSize, Is.EqualTo(group.Size));
            Assert.That(Width(LongText, group.Size, baloo), Is.LessThanOrEqualTo(width + 0.01f));
            if (group.Size < Max) Assert.That(Width(LongText, group.Size + 1f, baloo), Is.GreaterThan(width));
        }
    }
}
