using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.Purchases;
using NUnit.Framework;
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

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(view.gameObject);

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
