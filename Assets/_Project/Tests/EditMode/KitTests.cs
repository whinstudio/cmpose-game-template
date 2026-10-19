using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class KitTests
    {
        private readonly List<Object> made = new List<Object>();
        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            made.Add(canvas);
            // A game Compass dressed ships a skin: these tests start without one either way
            GameSkin.ForTests(GameSkin.Parse("{}", _ => null, _ => null));
        }

        [TearDown]
        public void Clean()
        {
            GameSkin.ForTests(null);
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private Palette MakePalette(float seed)
        {
            var palette = ScriptableObject.CreateInstance<Palette>();
            foreach (ThemeRole role in Enum.GetValues(typeof(ThemeRole)))
                palette.Set(role, new Color(seed, (int)role / 20f, 1f - seed, 1f));
            made.Add(palette);
            return palette;
        }

        private ThemeStyle MakeStyle(float radius, SurfaceStyle surface, float shadow = 0.4f, MotionProfile motion = MotionProfile.Snappy)
        {
            var style = ScriptableObject.CreateInstance<ThemeStyle>();
            var serialized = new SerializedObject(style);
            serialized.FindProperty("cornerRadius").floatValue = radius;
            serialized.FindProperty("surface").enumValueIndex = (int)surface;
            serialized.FindProperty("shadowStrength").floatValue = shadow;
            serialized.FindProperty("motion").enumValueIndex = (int)motion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            made.Add(style);
            return style;
        }

        private FakeThemeSource Theme(ThemeStyle style) => new FakeThemeSource { Current = MakePalette(0.2f), Style = style };

        private static Image Part(Component kit, string name) => kit.transform.Find(name).GetComponent<Image>();

        // The skin of SkinSprites.Json (a pixel pack; json: another) with its sprites made here; font: the skin's font
        // source (null: none)
        private GameSkin Skin(Font font = null, string json = null)
        {
            var sprites = new Dictionary<string, Sprite>();
            var skin = GameSkin.Parse(json ?? SkinSprites.Json, path =>
            {
                if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = SkinSprites.Make(made, path.Substring(path.LastIndexOf('/') + 1));
                return sprite;
            }, _ => font);
            GameSkin.ForTests(skin);
            return skin;
        }

        [Test]
        public void Kit_sprites_load_from_resources_as_nine_slices()
        {
            Assert.That(KitSprites.Rounded, Is.Not.Null, "Resources/Kit/Rounded: run KitSpriteBuilder.BuildKitSprites");
            Assert.That(KitSprites.Shadow, Is.Not.Null, "Resources/Kit/Shadow: run KitSpriteBuilder.BuildKitSprites");
            Assert.That(KitSprites.Rounded.border, Is.EqualTo(Vector4.one * KitSprites.RoundedRadius));
            Assert.That(KitSprites.Shadow.border, Is.EqualTo(Vector4.one * (KitSprites.ShadowRadius + KitSprites.ShadowBlur)));
            Assert.That(KitSprites.Rounded.pixelsPerUnit, Is.EqualTo(100f));
            Assert.That(KitSprites.Shadow.pixelsPerUnit, Is.EqualTo(100f));
            foreach (var (sprite, name) in new[] { (KitSprites.Star, "Star"), (KitSprites.Gear, "Gear"), (KitSprites.Info, "Info") })
                Assert.That(sprite, Is.Not.Null, $"Resources/Kit/{name}: run KitSpriteBuilder.BuildKitSprites");
        }

        [Test]
        public void ButtonUsesStyleRadius()
        {
            var button = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Primary);
            button.Bind(Theme(MakeStyle(28f, SurfaceStyle.Soft)));

            Assert.That(button.Radius, Is.EqualTo(28f).Within(0.01f));
            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.Button.targetGraphic, Is.SameAs(button.Body));
        }

        [Test]
        public void Button_radius_follows_the_variant()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Flat));
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon);
            var play = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Play);
            icon.Bind(theme);
            play.Bind(theme);

            Assert.That(icon.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(play.Radius, Is.EqualTo(32f).Within(0.01f));
        }

        [Test]
        public void A_square_style_gives_square_buttons()
        {
            var button = KitFactory.Button(canvas.transform, "B", KitButtonVariant.Primary);
            button.Bind(Theme(MakeStyle(0f, SurfaceStyle.Flat)));

            Assert.That(button.Radius, Is.EqualTo(0f));
            Assert.That(button.Body.sprite, Is.Null);
        }

        [Test]
        public void Surfaces_turn_on_their_layers()
        {
            var soft = KitFactory.Button(canvas.transform, "Soft", KitButtonVariant.Primary);
            var raised = KitFactory.Button(canvas.transform, "Raised", KitButtonVariant.Primary);
            var flat = KitFactory.Button(canvas.transform, "Flat", KitButtonVariant.Primary);
            soft.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft, 0.35f)));
            raised.Bind(Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f)));
            flat.Bind(Theme(MakeStyle(20f, SurfaceStyle.Flat)));

            Assert.That(Part(soft, "Shadow").gameObject.activeSelf, Is.True, "soft: shadow");
            Assert.That(Part(soft, "Edge").gameObject.activeSelf, Is.False, "soft: edge");
            Assert.That(Part(soft, "Shadow").GetComponent<ThemedGraphic>().Opacity, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(Part(raised, "Shadow").gameObject.activeSelf, Is.False, "raised: shadow");
            var edge = Part(raised, "Edge");
            Assert.That(edge.gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(edge.rectTransform.offsetMin.y, Is.LessThan(-5f), "raised: the base shows below the body");
            Assert.That(edge.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Primary));
            Assert.That(edge.GetComponent<ThemedGraphic>().Shade, Is.GreaterThan(0f), "raised: the base is darker");
            Assert.That(Part(raised, "Shine").gameObject.activeSelf, Is.True, "raised: shine");
            foreach (var layer in new[] { "Shadow", "Edge", "Shine" })
                Assert.That(Part(flat, layer).gameObject.activeSelf, Is.False, $"flat: {layer}");
        }

        // A raised Secondary's outline rims the top too (white on a white card would read as cut off), the base shows below
        [Test]
        public void A_raised_outline_rims_the_top_of_the_button()
        {
            var secondary = KitFactory.Button(canvas.transform, "Secondary", KitButtonVariant.Secondary);
            secondary.Bind(Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f)));
            var edge = Part(secondary, "Edge").rectTransform;
            Assert.That(edge.offsetMax.y, Is.GreaterThan(0f), "rims the top");
            Assert.That(edge.offsetMin.y, Is.LessThan(-5f), "the base shows below");
        }

        [Test]
        public void Variants_colour_body_and_label_with_readable_pairs()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            var expected = new Dictionary<KitButtonVariant, (ThemeRole Body, ThemeRole Label)>
            {
                { KitButtonVariant.Primary, (ThemeRole.Primary, ThemeRole.OnPrimary) },
                { KitButtonVariant.Secondary, (ThemeRole.Surface, ThemeRole.Text) },
                { KitButtonVariant.Icon, (ThemeRole.Primary, ThemeRole.OnPrimary) },
                { KitButtonVariant.Play, (ThemeRole.Accent, ThemeRole.OnAccent) }
            };
            foreach (var pair in expected)
            {
                var button = KitFactory.Button(canvas.transform, pair.Key.ToString(), pair.Key);
                button.Bind(theme);
                Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(pair.Value.Body)), $"{pair.Key}: body");
                Assert.That(button.Label.color, Is.EqualTo(theme.Current.Get(pair.Value.Label)), $"{pair.Key}: label");
            }
            var secondary = KitFactory.Button(canvas.transform, "Outlined", KitButtonVariant.Secondary);
            secondary.Bind(theme);
            Assert.That(Part(secondary, "Edge").gameObject.activeSelf, Is.True, "secondary: outline");
            Assert.That(Part(secondary, "Edge").GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control));
        }

        [Test]
        public void Play_breathes_and_every_button_springs()
        {
            var play = KitFactory.Button(canvas.transform, "Play", KitButtonVariant.Play);
            var primary = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);

            Assert.That(play.GetComponent<IdlePulse>(), Is.Not.Null);
            Assert.That(play.GetComponent<PressFeedback>(), Is.Not.Null);
            Assert.That(primary.GetComponent<IdlePulse>(), Is.Null);
            Assert.That(primary.GetComponent<PressFeedback>(), Is.Not.Null);
        }

        // The glyph sits in its disc at the button's left, clear of the label; Secondary draws it in Primary (3:1 on
        // Surface in every family), the others in their label colour
        [Test]
        public void An_icon_button_shows_its_glyph_beside_the_label()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            foreach (var variant in new[] { KitButtonVariant.Secondary, KitButtonVariant.Primary })
            {
                var button = KitFactory.Button(canvas.transform, variant.ToString(), variant, KitSprites.Gear);
                button.Bind(theme);
                Assert.That(button.Icon, Is.Not.Null, $"{variant}: icon");
                Assert.That(button.Icon.sprite, Is.SameAs(KitSprites.Gear), $"{variant}: sprite");
                Assert.That(button.Icon.raycastTarget, Is.False, $"{variant}: the icon takes no touches");
                var role = variant == KitButtonVariant.Secondary ? ThemeRole.Primary : ThemeRole.OnPrimary;
                Assert.That(button.Icon.color, Is.EqualTo(theme.Current.Get(role)), $"{variant}: icon colour");
                var back = Part(button, "IconBack");
                Assert.That(back.GetComponent<ThemedGraphic>().Role, Is.EqualTo(role), $"{variant}: disc role");
                Assert.That(back.GetComponent<ThemedGraphic>().Opacity, Is.EqualTo(KitButton.IconBackOpacity), $"{variant}: disc is faint");
                var discRight = back.rectTransform.anchoredPosition.x + back.rectTransform.sizeDelta.x / 2f;
                Assert.That(button.Label.margin.x, Is.GreaterThan(discRight), $"{variant}: the label starts after the disc");
                Assert.That(button.Label.transform.GetSiblingIndex(), Is.EqualTo(button.transform.childCount - 1), $"{variant}: label on top");
            }
            Assert.That(KitFactory.Button(canvas.transform, "Plain", KitButtonVariant.Secondary).Icon, Is.Null, "no icon by default");
        }

        // Decor is background: faint, untouchable, themed, in the shape asked for
        [Test]
        public void Decor_is_a_faint_untouchable_themed_shape()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            foreach (KitDecorShape shape in Enum.GetValues(typeof(KitDecorShape)))
            {
                var decor = KitFactory.Decor(canvas.transform, shape.ToString(), shape, ThemeRole.Accent, 0.2f, 120f, 15f);
                decor.Bind(theme);
                var image = decor.GetComponent<Image>();
                Assert.That(image.raycastTarget, Is.False, $"{shape}: no touches");
                Assert.That(image.color, Is.EqualTo(ThemedGraphic.Tone(theme.Current.Get(ThemeRole.Accent), 0f, 0.2f)), $"{shape}: colour");
                Assert.That(decor.Shape, Is.EqualTo(shape));
                Assert.That(image.sprite, Is.SameAs(shape == KitDecorShape.Star ? KitSprites.Star : KitSprites.Rounded), $"{shape}: sprite");
                Assert.That(decor.transform.localEulerAngles.z, Is.EqualTo(15f).Within(0.01f), $"{shape}: tilt");
            }
        }

        // A drifting shape comes back to where it was placed when it stops
        [Test]
        public void Decor_drifts_around_its_place_and_returns_to_it()
        {
            var decor = KitFactory.Decor(canvas.transform, "Circle", KitDecorShape.Circle, ThemeRole.Primary, 0.1f, 200f, 0f, 20f);
            decor.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            var rect = (RectTransform)decor.transform;
            rect.anchoredPosition = new Vector2(10f, 30f);
            var lowest = float.MaxValue;
            var highest = float.MinValue;
            for (var i = 0; i < 200; i++)
            {
                decor.Advance(0.05f);
                lowest = Mathf.Min(lowest, rect.anchoredPosition.y);
                highest = Mathf.Max(highest, rect.anchoredPosition.y);
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(10f), "drifts only up and down");
            }
            Assert.That(highest, Is.EqualTo(50f).Within(0.5f), "up by its drift");
            Assert.That(lowest, Is.EqualTo(10f).Within(0.5f), "down by its drift");
            decor.Rest();
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(10f, 30f)), "back in place");
        }

        [Test]
        public void Every_kit_graphic_has_a_theme_role()
        {
            var parts = new Component[]
            {
                KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Secondary),
                KitFactory.Card(canvas.transform, "Card"),
                KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Bottom, 900f),
                KitFactory.Switch(canvas.transform, "Switch"),
                KitFactory.Slider(canvas.transform, "Slider"),
                KitFactory.Segmented(canvas.transform, "Segmented", 3),
                KitFactory.Badge(canvas.transform, "Badge", KitBadgeTone.Primary),
                KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 96f),
                KitFactory.Button(canvas.transform, "IconButton", KitButtonVariant.Secondary, KitSprites.Info),
                KitFactory.Decor(canvas.transform, "Decor", KitDecorShape.Star, ThemeRole.Accent, 0.5f, 60f)
            };
            foreach (var part in parts)
            foreach (var graphic in part.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.GetComponent<ThemedGraphic>(), Is.Not.Null, $"{part.name}/{graphic.name}");
        }

        [Test]
        public void SwitchRepaintsOnThemeChange()
        {
            var kit = KitFactory.Switch(canvas.transform, "Switch");
            var first = MakePalette(0.1f);
            var second = MakePalette(0.9f);
            var theme = new FakeThemeSource { Current = first, Style = MakeStyle(20f, SurfaceStyle.Soft) };
            kit.Bind(theme);
            Assert.That(kit.Track.color, Is.EqualTo(first.Get(ThemeRole.Primary)), "on, first palette");
            Assert.That(kit.Knob.color, Is.EqualTo(first.Get(ThemeRole.Surface)), "knob, first palette");

            theme.Switch(second);
            Assert.That(kit.Track.color, Is.EqualTo(second.Get(ThemeRole.Primary)), "on, second palette");
            Assert.That(kit.Knob.color, Is.EqualTo(second.Get(ThemeRole.Surface)), "knob, second palette");

            kit.Toggle.isOn = false;
            theme.Switch(first);
            Assert.That(kit.Track.color, Is.EqualTo(first.Get(ThemeRole.Control)), "off, first palette");
            Assert.That(kit.Position, Is.EqualTo(0f));
        }

        [Test]
        public void Slider_parts_are_round_and_the_fill_ends_under_the_handle()
        {
            var kit = KitFactory.Slider(canvas.transform, "Slider");
            kit.Bind(Theme(MakeStyle(14f, SurfaceStyle.Raised)));

            Assert.That(KitShape.RadiusOf(kit.Rail), Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(KitShape.RadiusOf(kit.Handle), Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(kit.Slider.fillRect, Is.SameAs(kit.Fill.rectTransform));
            Assert.That(kit.Fill.rectTransform.offsetMax.x, Is.EqualTo(0f));
            Assert.That(kit.Rail.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Control));
            Assert.That(kit.Handle.GetComponent<ThemedGraphic>().Role, Is.EqualTo(ThemeRole.Primary));
        }

        [Test]
        public void CountUpReachesTarget()
        {
            var count = KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 96f);
            var reached = 0;
            count.Reached += () => reached++;

            count.CountTo(250, 0.5f);
            count.Advance(0.25f);
            Assert.That(count.Value, Is.GreaterThan(0).And.LessThan(250), "half way");
            Assert.That(reached, Is.Zero);

            count.Advance(0.3f);
            Assert.That(count.Value, Is.EqualTo(250));
            Assert.That(count.Counting, Is.False);
            Assert.That(count.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("250"));
            Assert.That(reached, Is.EqualTo(1));

            count.Advance(1f);
            Assert.That(reached, Is.EqualTo(1), "reaching is announced once");
            Assert.That(count.transform.localScale, Is.EqualTo(Vector3.one), "the pop settles");
        }

        [Test]
        public void CountUp_counts_down_and_set_value_is_immediate()
        {
            var count = KitFactory.CountUp(canvas.transform, "Coins", TextRole.Title, 64f);
            count.SetValue(90);
            Assert.That(count.Value, Is.EqualTo(90));
            Assert.That(count.Counting, Is.False);

            count.CountTo(40, 0.2f);
            count.Advance(0.1f);
            Assert.That(count.Value, Is.InRange(41, 89));
            count.Advance(0.2f);
            Assert.That(count.Value, Is.EqualTo(40));
        }

        [Test]
        public void MotionDurationsFollowProfile()
        {
            var calm = UiMotion.Timings(MotionProfile.Calm);
            var snappy = UiMotion.Timings(MotionProfile.Snappy);
            var bouncy = UiMotion.Timings(MotionProfile.Bouncy);

            Assert.That(calm.Transition, Is.GreaterThan(bouncy.Transition));
            Assert.That(bouncy.Transition, Is.GreaterThan(snappy.Transition));
            Assert.That(calm.Sheet, Is.GreaterThan(bouncy.Sheet));
            Assert.That(bouncy.Sheet, Is.GreaterThan(snappy.Sheet));
            Assert.That(calm.Press, Is.GreaterThan(snappy.Press));
            Assert.That(calm.Breath, Is.GreaterThan(snappy.Breath));
            Assert.That(snappy.Breath, Is.GreaterThan(bouncy.Breath));
            Assert.That(bouncy.PressScale, Is.LessThan(snappy.PressScale));
            Assert.That(snappy.PressScale, Is.LessThan(calm.PressScale));
            Assert.That(bouncy.ReleaseEase, Is.EqualTo(Ease.Spring));
            Assert.That(calm.ReleaseEase, Is.EqualTo(Ease.OutCubic));
            // A paused game still animates: nothing in the kit takes more than half a second, except counting
            foreach (var t in new[] { calm, snappy, bouncy })
            {
                Assert.That(t.Press + t.Release, Is.LessThanOrEqualTo(0.5f));
                Assert.That(t.Sheet, Is.LessThanOrEqualTo(0.5f));
            }
            Assert.That(UiMotion.ProfileOf(null), Is.EqualTo(MotionProfile.Snappy));
            Assert.That(UiMotion.ProfileOf(Theme(MakeStyle(20f, SurfaceStyle.Soft, motion: MotionProfile.Calm))), Is.EqualTo(MotionProfile.Calm));
        }

        [Test]
        public void Every_ease_starts_at_zero_and_ends_at_one()
        {
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
            {
                Assert.That(UiMotion.Evaluate(ease, 0f), Is.EqualTo(0f), $"{ease} at 0");
                Assert.That(UiMotion.Evaluate(ease, 1f), Is.EqualTo(1f), $"{ease} at 1");
                Assert.That(UiMotion.Evaluate(ease, -1f), Is.EqualTo(0f), $"{ease} before");
                Assert.That(UiMotion.Evaluate(ease, 2f), Is.EqualTo(1f), $"{ease} after");
            }
            var peak = 0f;
            for (var t = 0f; t <= 1f; t += 0.01f) peak = Mathf.Max(peak, UiMotion.Evaluate(Ease.Spring, t));
            Assert.That(peak, Is.InRange(1.1f, 1.3f), "the spring overshoots");
            Assert.That(UiMotion.Evaluate(Ease.OutCubic, 0.5f), Is.LessThan(1f), "OutCubic does not");
            Assert.That(UiMotion.Punch(0f), Is.EqualTo(0f));
            Assert.That(UiMotion.Punch(1f), Is.EqualTo(0f));
        }

        [Test]
        public void A_tween_steps_to_its_target_and_rests()
        {
            var tween = new KitTween();
            tween.Start(0f, 1f, 0.2f, Ease.Linear);
            Assert.That(tween.Step(0.1f), Is.True);
            Assert.That(tween.Value, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(tween.Step(0.2f), Is.True);
            Assert.That(tween.Value, Is.EqualTo(1f));
            Assert.That(tween.Running, Is.False);
            Assert.That(tween.Step(0.1f), Is.False);

            tween.Start(0f, 1f, 0f, Ease.Spring);
            Assert.That(tween.Running, Is.False, "a zero-length tween is at its end at once");
            Assert.That(tween.Value, Is.EqualTo(1f));
        }

        [Test]
        public void A_sheet_is_built_closed_and_opens_and_closes_at_once_without_animation()
        {
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Bottom, 900f);
            sheet.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That(sheet.IsOpen, Is.False);

            var opened = 0;
            var closed = 0;
            sheet.Opened += () => opened++;
            sheet.Closed += () => closed++;
            sheet.SetOpen(true, false);
            Assert.That(sheet.gameObject.activeSelf, Is.True);
            Assert.That(sheet.Progress, Is.EqualTo(1f));
            Assert.That(sheet.Dim.alpha, Is.EqualTo(1f));
            Assert.That(sheet.Panel.anchoredPosition.y, Is.EqualTo(-KitFactory.SheetOverhang).Within(0.01f));

            sheet.SetOpen(false, false);
            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That((opened, closed), Is.EqualTo((1, 1)));
        }

        [Test]
        public void Segmented_moves_its_indicator_and_recolours_labels()
        {
            var kit = KitFactory.Segmented(canvas.transform, "Picker", 3);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            kit.Bind(theme);

            kit.Select(2, false);
            Assert.That(kit.Selected, Is.EqualTo(2));
            Assert.That(kit.Indicator.rectTransform.anchorMin.x, Is.EqualTo(2f / 3f).Within(0.001f));
            Assert.That(kit.Label(2).color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(kit.Label(0).color, Is.EqualTo(theme.Current.Get(ThemeRole.Text)));
        }

        [Test]
        public void Segmented_refuses_bad_counts_and_indexes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => KitFactory.Segmented(canvas.transform, "One", 1));
            var kit = KitFactory.Segmented(canvas.transform, "Two", 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => kit.Select(2, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => kit.Select(-1, false));
            Assert.That(kit.Selected, Is.Zero);
        }

        [Test]
        public void A_disabled_button_does_not_press()
        {
            var kit = KitFactory.Button(canvas.transform, "Off", KitButtonVariant.Primary);
            kit.Button.interactable = false;
            var press = kit.GetComponent<PressFeedback>();

            press.OnPointerDown(new PointerEventData(null));
            Assert.That(press.Busy, Is.False);
            press.OnPointerUp(new PointerEventData(null));
            Assert.That(press.Busy, Is.False);
        }

        [Test]
        public void Badge_takes_its_tone()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Soft));
            var badge = KitFactory.Badge(canvas.transform, "New", KitBadgeTone.Accent);
            badge.Bind(theme);
            badge.SetText("NEW");

            Assert.That(badge.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Accent)));
            Assert.That(badge.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnAccent)));
            Assert.That(badge.Label.text, Is.EqualTo("NEW"));
        }

        [Test]
        public void A_tone_darkens_and_fades_the_role_colour()
        {
            var colour = new Color(0.8f, 0.6f, 0.4f, 0.5f);
            Assert.That(ThemedGraphic.Tone(colour, 0f, 1f), Is.EqualTo(colour));
            var toned = ThemedGraphic.Tone(colour, 0.5f, 0.5f);
            Assert.That(toned.r, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(toned.a, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void The_binder_binds_kit_elements()
        {
            var button = KitFactory.Button(canvas.transform, "B", KitButtonVariant.Primary);
            var theme = Theme(MakeStyle(24f, SurfaceStyle.Soft));
            new UiBinder(new Kturker.UnityBase.Core.Localization.TableLocalizer(new FakeStringSource(), new FakeLog(), false), theme).BindTree(canvas);

            Assert.That(button.Radius, Is.EqualTo(24f).Within(0.01f));
            Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
        }

        [Test]
        public void Unskinned_kit_is_unchanged()
        {
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Center, 600f);
            button.Bind(theme);
            card.Bind(theme);
            sheet.Bind(theme);

            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(button.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
            Assert.That(button.Label.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(Part(button, "Edge").gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(Part(button, "Shine").gameObject.activeSelf, Is.True, "raised: shine");
            Assert.That(card.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(card.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)));
            var panel = sheet.Panel.GetComponent<KitCard>();
            Assert.That(panel.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(panel.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Surface)));
            Assert.That(theme.Style.FontFor(TextRole.Display), Is.SameAs(theme.Style.DisplayFont));

            button.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(KitSprites.Rounded), "no pressed sprite without a skin");
        }

        // Kenney sprites keep their own colours: the body is white whatever the palette, the label takes the skin's colour
        [Test]
        public void Skinned_button_uses_sliced_sprite_and_hides_layers()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon, KitSprites.Gear);
            button.Bind(theme);
            icon.Bind(theme);

            var entry = skin.Button(KitButtonVariant.Primary);
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal));
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f));
            foreach (var layer in new[] { "Shadow", "Edge", "Shine" })
                Assert.That(Part(button, layer).gameObject.activeSelf, Is.False, layer);
            Assert.That(icon.Body.sprite, Is.SameAs(skin.Button(KitButtonVariant.Icon).Normal), "icon variant");
            Assert.That(icon.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "no pill on a skinned icon button");

            theme.Switch(MakePalette(0.9f));
            Assert.That(button.Body.color, Is.EqualTo(Color.white), "body after a palette change");
            Assert.That(button.Label.color, Is.EqualTo(entry.Text), "label after a palette change");
            Assert.That(icon.Icon.color, Is.EqualTo(skin.Button(KitButtonVariant.Icon).Text), "glyph");
        }

        // A skin without an icon entry dresses the other buttons; icon buttons keep today's round, themed layers
        [Test]
        public void Icon_button_without_skin_entry_keeps_kit_shape()
        {
            GameSkin.ForTests(GameSkin.Parse(SkinSprites.WithoutIcon, path => SkinSprites.Make(made, path), _ => null));
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var icon = KitFactory.Button(canvas.transform, "Icon", KitButtonVariant.Icon, KitSprites.Gear);
            var primary = KitFactory.Button(canvas.transform, "Primary", KitButtonVariant.Primary);
            icon.Bind(theme);
            primary.Bind(theme);

            Assert.That(primary.Body.sprite, Is.Not.SameAs(KitSprites.Rounded), "the primary button is dressed");
            Assert.That(icon.Body.sprite, Is.SameAs(KitSprites.Rounded));
            Assert.That(icon.Radius, Is.EqualTo(KitSprites.Pill).Within(0.5f));
            Assert.That(icon.Body.color, Is.EqualTo(theme.Current.Get(ThemeRole.Primary)));
            Assert.That(icon.Icon.color, Is.EqualTo(theme.Current.Get(ThemeRole.OnPrimary)));
            Assert.That(Part(icon, "Edge").gameObject.activeSelf, Is.True, "raised: edge");
            Assert.That(Part(icon, "Shine").gameObject.activeSelf, Is.True, "raised: shine");

            icon.OnPointerDown(new PointerEventData(null));
            Assert.That(icon.Body.sprite, Is.SameAs(KitSprites.Rounded), "no pressed sprite");
        }

        [Test]
        public void Skinned_button_swaps_sprite_while_pressed()
        {
            var skin = Skin();
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Secondary);
            button.Bind(Theme(MakeStyle(20f, SurfaceStyle.Soft)));
            var kit = button;
            var entry = skin.Button(KitButtonVariant.Secondary);

            kit.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Pressed), "down");
            kit.OnPointerUp(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal), "up");

            button.Button.interactable = false;
            kit.OnPointerDown(new PointerEventData(null));
            Assert.That(button.Body.sprite, Is.SameAs(entry.Normal), "a disabled button does not press");
        }

        [Test]
        public void Skinned_card_and_dialog_use_panel_sprites()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var card = KitFactory.Card(canvas.transform, "Card", true);
            var bottom = KitFactory.Sheet(canvas.transform, "Bottom", SheetPlacement.Bottom, 900f);
            var dialog = KitFactory.Sheet(canvas.transform, "Dialog", SheetPlacement.Center, 600f);
            var inner = KitFactory.Card(dialog.Content, "Inner");
            card.Bind(theme);
            bottom.Bind(theme);
            dialog.Bind(theme);
            theme.Switch(MakePalette(0.7f));

            var cases = new[]
            {
                (card, skin.Card, "card"),
                (bottom.Panel.GetComponent<KitCard>(), skin.Sheet, "bottom sheet"),
                (dialog.Panel.GetComponent<KitCard>(), skin.Dialog, "dialog"),
                (inner, skin.Card, "a card in a dialog")
            };
            foreach (var (kit, sprite, what) in cases)
            {
                Assert.That(kit.Body.sprite, Is.SameAs(sprite), what);
                Assert.That(kit.Body.type, Is.EqualTo(Image.Type.Sliced), what);
                Assert.That(kit.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), what);
                Assert.That(kit.Body.color, Is.EqualTo(Color.white), what);
                foreach (var layer in new[] { "Shadow", "Edge" })
                    Assert.That(Part(kit, layer).gameObject.activeSelf, Is.False, $"{what}: {layer}");
            }
        }

        // A pixel pack's edges carry patterns (stitches, studs) that a stretched slice smears: drawn tiled, they repeat
        [Test]
        public void Pixel_skin_draws_tiled()
        {
            var skin = Skin();
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            button.Bind(theme);
            card.Bind(theme);

            Assert.That(skin.Pixel, Is.True);
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Tiled), "button");
            Assert.That(button.Body.fillCenter, Is.True, "button centre");
            Assert.That(button.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "button scale");
            Assert.That(card.Body.type, Is.EqualTo(Image.Type.Tiled), "card");
            Assert.That(card.Body.pixelsPerUnitMultiplier, Is.EqualTo(0.25f).Within(1e-5f), "card scale");
        }

        [Test]
        public void Soft_skin_draws_sliced()
        {
            var skin = Skin(json: SkinSprites.Soft);
            var theme = Theme(MakeStyle(20f, SurfaceStyle.Raised, 0.5f));
            var button = KitFactory.Button(canvas.transform, "Button", KitButtonVariant.Primary);
            var card = KitFactory.Card(canvas.transform, "Card");
            button.Bind(theme);
            card.Bind(theme);

            Assert.That(skin.Pixel, Is.False);
            Assert.That(button.Body.type, Is.EqualTo(Image.Type.Sliced), "button");
            Assert.That(card.Body.type, Is.EqualTo(Image.Type.Sliced), "card");
        }

        [Test]
        public void Display_font_comes_from_skin()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Core/Presentation/Fonts/Inter/Inter-Regular.ttf");
            Assert.That(source, Is.Not.Null);
            var skin = Skin(source);
            var style = MakeStyle(20f, SurfaceStyle.Soft);

            var display = style.FontFor(TextRole.Display);
            Assert.That(display, Is.Not.Null);
            made.Add(display);
            Assert.That(display, Is.SameAs(skin.DisplayFont));
            Assert.That(style.FontFor(TextRole.Title), Is.SameAs(display));
            Assert.That(style.FontFor(TextRole.Body), Is.SameAs(style.BodyFont), "body text keeps the family's font");
            Assert.That(display.atlasPopulationMode, Is.EqualTo(TMPro.AtlasPopulationMode.Dynamic));
            var fallback = ThemeStyle.Load().DisplayFont;
            Assert.That(display.fallbackFontAssetTable, Is.EqualTo(new List<TMPro.TMP_FontAsset> { fallback }), "falls back to the theme's display font");
        }
    }
}
