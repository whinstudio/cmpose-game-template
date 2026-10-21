using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.Purchases;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Purchases.Tests
{
    // The paywall prefab (SceneBuilder): the chosen package stands out by its row colour and a wide side bar; the plans
    // card is as tall as the packages shown
    public sealed class PaywallViewTests
    {
        private PaywallView view;

        [SetUp]
        public void SetUp() => view = Object.Instantiate(Resources.Load<PaywallView>("ShellPurchases/Paywall"));

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            TestSkin.Use(null);
            Object.DestroyImmediate(view.gameObject);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private Transform Row(int i) => view.transform.Find($"Panel/SafeArea/Plans/Row{i}");

        private ThemeRole RoleOf(int i) => Row(i).GetComponent<ThemedGraphic>().Role;

        [Test]
        public void The_selected_row_takes_the_surface_variant_and_the_others_the_track_colour()
        {
            view.Select(1);
            for (var i = 0; i < PaywallView.Rows; i++)
            {
                Assert.That(RoleOf(i), Is.EqualTo(i == 1 ? ThemeRole.SurfaceVariant : ThemeRole.Track), $"row {i}");
                Assert.That(Row(i).Find("Selected").gameObject.activeSelf, Is.EqualTo(i == 1), $"row {i} bar");
            }
        }

        [Test]
        public void A_new_selection_returns_the_old_row_to_normal()
        {
            view.Select(1);
            view.Select(2);
            Assert.That(RoleOf(1), Is.EqualTo(ThemeRole.Track));
            Assert.That(RoleOf(2), Is.EqualTo(ThemeRole.SurfaceVariant));
            Assert.That(Row(1).Find("Selected").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void The_selection_bar_is_wide_enough_to_see()
        {
            var bar = (RectTransform)Row(0).Find("Selected");
            Assert.That(bar.sizeDelta.x, Is.GreaterThanOrEqualTo(24f));
        }

        private RectTransform Plans => (RectTransform)view.transform.Find("Panel/SafeArea/Plans");

        private RectTransform Part(string name) => (RectTransform)Plans.Find(name);

        // Bottom and top of a rect in the card's units (the canvas around it may be scaled)
        private (float Bottom, float Top) Edges(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (Plans.InverseTransformPoint(corners[0]).y, Plans.InverseTransformPoint(corners[1]).y);
        }

        private float LaidOutHeight(int packages)
        {
            view.ShowPackages(Packages(packages), 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Plans);
            return Plans.rect.height;
        }

        private static PaywallRow[] Packages(int count)
        {
            var list = new PaywallRow[count];
            for (var i = 0; i < count; i++) list[i] = new PaywallRow("purchases.period.monthly", "$1.99", null, null, null);
            return list;
        }

        [Test]
        public void Continue_follows_the_last_package_shown_whatever_their_number()
        {
            var spacing = Plans.GetComponent<VerticalLayoutGroup>().spacing;
            for (var count = 1; count <= PaywallView.Rows; count++)
            {
                LaidOutHeight(count);
                var lastRow = Edges(Part($"Row{count - 1}")).Bottom;
                var buy = Edges(Part("Buy")).Top;
                Assert.That(lastRow - buy, Is.EqualTo(spacing).Within(0.5f), $"{count} packages: gap above Continue");
            }
        }

        [Test]
        public void Fewer_packages_make_a_shorter_card()
        {
            var one = LaidOutHeight(1);
            var all = LaidOutHeight(PaywallView.Rows);
            var row = Part("Row0").rect.height;
            var spacing = Plans.GetComponent<VerticalLayoutGroup>().spacing;
            Assert.That(all - one, Is.EqualTo((PaywallView.Rows - 1) * (row + spacing)).Within(0.5f));
        }

        // The paywall laid out as a 20:9 phone (1080 x 2400) shows it, the narrowest column: the size its canvas scaler
        // gives that screen, on a world-space canvas (an overlay canvas takes the editor's screen size)
        private void LayOutOnATallPhone()
        {
            var scaler = view.GetComponent<CanvasScaler>();
            var reference = scaler.referenceResolution;
            var scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(1080f / reference.x, 2f), Mathf.Log(2400f / reference.y, 2f),
                scaler.matchWidthOrHeight));
            Object.DestroyImmediate(scaler);
            view.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)view.transform).sizeDelta = new Vector2(1080f / scale, 2400f / scale);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Plans);
        }

        // The device check: "Bonus: +400 gold, +3 Freeze, +3 Shield, +3 Lantern" filled the column's width and ran into
        // the skin's card frame on both sides. It, and three boosters with 12-letter names and a 5-digit amount, fit
        // inside the inset on two lines at most.
        [TestCase("Bonus: +400 gold, +3 Freeze, +3 Shield, +3 Lantern")]
        [TestCase("Bonus: +12345 gold, +3 Freezeblocks, +3 Shieldarmour, +3 Lanternlight")]
        public void A_long_bonus_line_stays_inside_the_card_frame(string line)
        {
            view.ShowPackages(Packages(PaywallView.Rows), 0);
            view.ShowBonus(line);
            LayOutOnATallPhone();
            var text = Part("Bonus").GetComponent<TMP_Text>();
            text.ForceMeshUpdate();

            var padding = Plans.GetComponent<VerticalLayoutGroup>().padding;
            var bounds = text.textBounds;
            var left = Plans.InverseTransformPoint(text.transform.TransformPoint(bounds.min)).x;
            var right = Plans.InverseTransformPoint(text.transform.TransformPoint(bounds.max)).x;
            Assert.That(text.isTextOverflowing, Is.False);
            Assert.That(text.textInfo.lineCount, Is.InRange(1, 2));
            Assert.That(left, Is.GreaterThanOrEqualTo(Plans.rect.xMin + padding.left + PaywallView.BonusInset - 0.5f));
            Assert.That(right, Is.LessThanOrEqualTo(Plans.rect.xMax - padding.right - PaywallView.BonusInset + 0.5f));
        }

        [Test]
        public void A_short_bonus_line_keeps_one_line_at_full_size()
        {
            view.ShowBonus("Bonus: +400 coins");
            LayOutOnATallPhone();
            var text = Part("Bonus").GetComponent<TMP_Text>();
            text.ForceMeshUpdate();

            Assert.That(text.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(text.fontSize, Is.EqualTo(text.fontSizeMax));
        }

        // A pixel skin at sliceScale 3 whose panels are border source pixels thick on every side (a source pixel is 3 card units)
        private void Skin(float border)
        {
            const string json = @"{""ui"":{""pixel"":true,""sliceScale"":3,""buttons"":{
 ""primary"":{""normal"":""p"",""pressed"":""p"",""text"":""#FFFFFF""},""secondary"":{""normal"":""p"",""pressed"":""p"",""text"":""#222222""},
 ""play"":{""normal"":""p"",""pressed"":""p"",""text"":""#331100""}},""card"":""p"",""sheet"":""p"",""dialog"":""p""}}";
            var texture = new Texture2D(96, 96);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            made.Add(texture);
            made.Add(sprite);
            TestSkin.Use(GameSkin.Parse(json, _ => sprite, _ => null));
        }

        private void Bind()
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            made.Add(palette);
            var theme = new FakeThemeSource { Current = palette, Style = ThemeStyle.Load() };
            foreach (var element in view.GetComponentsInChildren<KitElement>(true)) element.Bind(theme);
        }

        // Where the bonus line's text area starts and ends, from the card's left and right edges
        private (float Left, float Right) BonusArea()
        {
            var text = Part("Bonus").GetComponent<TMP_Text>();
            var corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);
            var min = Plans.InverseTransformPoint(corners[0]).x;
            var max = Plans.InverseTransformPoint(corners[2]).x;
            return (min + text.margin.x - Plans.rect.xMin, Plans.rect.xMax - (max - text.margin.z));
        }

        // A frame thicker than the column's padding moves the whole column in (KitCard.Pad); the bonus line, which kept
        // BonusInset beyond the padding for that frame, does not add it again: it sits where the frame's inset is
        [Test]
        public void A_thick_skin_frame_moves_the_column_in_and_the_bonus_line_only_once()
        {
            Skin(30f);
            view.ShowPackages(Packages(PaywallView.Rows), 0);
            view.ShowBonus("Bonus: +400 coins");
            Bind();
            LayOutOnATallPhone();

            var padding = Plans.GetComponent<VerticalLayoutGroup>().padding;
            Assert.That((padding.left, padding.right), Is.EqualTo((102, 102)), "90-unit frame + 12");
            var (left, right) = BonusArea();
            Assert.That(left, Is.EqualTo(102f).Within(0.5f), "left");
            Assert.That(right, Is.EqualTo(102f).Within(0.5f), "right");
        }

        // A frame inside the column's padding plus BonusInset leaves the line where it was
        [Test]
        public void A_thin_skin_frame_leaves_the_bonus_line_where_it_was()
        {
            Skin(16f);
            view.ShowPackages(Packages(PaywallView.Rows), 0);
            view.ShowBonus("Bonus: +400 coins");
            Bind();
            LayOutOnATallPhone();

            var padding = Plans.GetComponent<VerticalLayoutGroup>().padding;
            Assert.That((padding.left, padding.right), Is.EqualTo((60, 60)), "48-unit frame + 12");
            var (left, right) = BonusArea();
            Assert.That(left, Is.EqualTo(30f + PaywallView.BonusInset).Within(0.5f), "left");
            Assert.That(right, Is.EqualTo(30f + PaywallView.BonusInset).Within(0.5f), "right");
        }

        [Test]
        public void The_legal_row_leaves_when_both_links_are_hidden()
        {
            view.SetLegal(false, false);
            Assert.That(Part("Legal").gameObject.activeSelf, Is.False);
            view.SetLegal(true, false);
            Assert.That(Part("Legal").gameObject.activeSelf, Is.True);
            Assert.That(Part("Legal/Terms").gameObject.activeSelf, Is.False);
        }
    }
}
