using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Builds the kit's components, centred in their parent at their default size; SceneBuilder builds the template's
    // screens with them and game code may build its own. Texts are left empty and get no LocalizedText (the caller
    // adds a key or writes them). Bind the result (KitElement.Bind or UiBinder.BindTree) to colour and shape it.
    public static class KitFactory
    {
        public static readonly Vector2 ButtonSize = new Vector2(600f, 160f);
        public static readonly Vector2 IconSize = new Vector2(140f, 140f);
        public static readonly Vector2 PlaySize = new Vector2(640f, 200f);
        public static readonly Vector2 SwitchSize = new Vector2(132f, 76f);
        public static readonly Vector2 SliderSize = new Vector2(800f, 72f);
        public const float SliderHandle = 64f;
        public const float SliderRail = 20f;
        // How far a bottom sheet's panel reaches below the screen's edge, so its lower corners never show
        public const float SheetOverhang = 80f;
        public const float SheetPadding = 48f;
        // An icon button's disc and glyph (units), the disc's gap from the button's left edge and from the label
        public const float IconBack = 84f;
        public const float IconGlyph = 50f;
        public const float IconInset = 26f;
        public const float IconGap = 10f;

        public static KitButton Button(Transform parent, string name, KitButtonVariant variant)
        {
            var root = Node(name, parent);
            Size(root, variant == KitButtonVariant.Icon ? IconSize : variant == KitButtonVariant.Play ? PlaySize : ButtonSize);
            var shadow = Layer("Shadow", root, ThemeRole.Scrim, false);
            var edge = Layer("Edge", root, KitButton.BodyRole(variant), false);
            var body = Layer("Body", root, KitButton.BodyRole(variant), true);
            var shine = Layer("Shine", root, ThemeRole.Picture, false);
            var labelSize = variant == KitButtonVariant.Play ? 72f : 56f;
            var label = Text("Label", root, KitButton.LabelRole(variant), TextRole.Title, labelSize);
            // Long labels (translations) shrink to fit the button instead of spilling out
            label.enableAutoSizing = true;
            label.fontSizeMax = labelSize;
            label.fontSizeMin = 30f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(24f, 4f, 24f, 4f);
            var button = root.AddComponent<Button>();
            button.targetGraphic = body;
            button.colors = PressColours();
            root.AddComponent<PressFeedback>();
            if (variant == KitButtonVariant.Play) root.AddComponent<IdlePulse>();
            var kit = root.AddComponent<KitButton>();
            kit.Init(variant, shadow, edge, body, shine, label);
            return kit;
        }

        // A button with icon (a white glyph sprite, e.g. KitSprites.Gear) in a disc at its left; the label keeps the rest
        public static KitButton Button(Transform parent, string name, KitButtonVariant variant, Sprite icon)
        {
            var kit = Button(parent, name, variant);
            var role = KitButton.IconRole(variant);
            var back = Layer("IconBack", kit.gameObject, role, false);
            var rect = back.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconBack, IconBack);
            rect.anchoredPosition = new Vector2(IconInset + IconBack / 2f, 0f);
            KitShape.Round(back, KitSprites.Pill);
            var glyph = Layer("Icon", back.gameObject, role, false);
            Size(glyph.gameObject, new Vector2(IconGlyph, IconGlyph));
            glyph.sprite = icon;
            glyph.preserveAspect = true;
            // Before the label, so the label stays the last child (drawn on top)
            back.transform.SetSiblingIndex(kit.Label.transform.GetSiblingIndex());
            var margin = kit.Label.margin;
            kit.Label.margin = new Vector4(IconInset + IconBack + IconGap, margin.y, margin.z, margin.w);
            kit.InitIcon(back, glyph);
            return kit;
        }

        // A background shape size units across in role at opacity, tilted by tilt degrees; it drifts drift units up and
        // down from where it is placed, starting phase (0..1) into its cycle
        public static KitDecor Decor(Transform parent, string name, KitDecorShape shape, ThemeRole role, float opacity, float size,
            float tilt = 0f, float drift = 14f, float phase = 0f)
        {
            var image = Layer(name, parent.gameObject, role, false);
            Size(image.gameObject, new Vector2(size, size));
            image.GetComponent<ThemedGraphic>().SetTone(0f, opacity);
            var decor = image.gameObject.AddComponent<KitDecor>();
            decor.Init(shape, drift, shape == KitDecorShape.Circle ? 0f : 4f, phase, tilt);
            return decor;
        }

        public static KitCard Card(Transform parent, string name, bool emphasis = false)
        {
            var root = Node(name, parent);
            Size(root, new Vector2(900f, 600f));
            var shadow = Layer("Shadow", root, ThemeRole.Scrim, false);
            var edge = Layer("Edge", root, ThemeRole.Surface, false);
            var body = Layer("Body", root, emphasis ? ThemeRole.SurfaceVariant : ThemeRole.Surface, true);
            var card = root.AddComponent<KitCard>();
            card.Init(emphasis, shadow, edge, body);
            return card;
        }

        // A closed sheet (inactive): Open shows it. A Bottom sheet is height units tall above the screen's bottom
        // edge, a Center one height units tall and 900 wide.
        public static KitSheet Sheet(Transform parent, string name, SheetPlacement placement, float height)
        {
            var root = Node(name, parent);
            Stretch(root);
            var dim = Layer("Dim", root, ThemeRole.Scrim, true);
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            var card = Card(root.transform, "Panel");
            var panel = (RectTransform)card.transform;
            if (placement == SheetPlacement.Bottom)
            {
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = new Vector2(1f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.sizeDelta = new Vector2(0f, height + SheetOverhang);
                panel.anchoredPosition = new Vector2(0f, -SheetOverhang);
            }
            else
            {
                Size(card.gameObject, new Vector2(900f, height));
            }
            var group = card.gameObject.AddComponent<CanvasGroup>();
            var content = (RectTransform)Node("Content", card.transform).transform;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            var bottom = placement == SheetPlacement.Bottom ? SheetOverhang + SheetPadding : SheetPadding;
            content.offsetMin = new Vector2(SheetPadding, bottom);
            content.offsetMax = new Vector2(-SheetPadding, -SheetPadding);
            var sheet = root.AddComponent<KitSheet>();
            sheet.Init(placement, dimGroup, panel, group, content);
            root.SetActive(false);
            return sheet;
        }

        public static KitSwitch Switch(Transform parent, string name)
        {
            var root = Node(name, parent);
            Size(root, SwitchSize);
            var track = Layer("Track", root, ThemeRole.Primary, true);
            var slot = (RectTransform)Node("KnobSlot", root.transform).transform;
            var knobSize = SwitchSize.y - 16f;
            slot.sizeDelta = new Vector2(knobSize, knobSize);
            var shadow = Layer("Shadow", slot.gameObject, ThemeRole.Scrim, false);
            var knob = Layer("Knob", slot.gameObject, ThemeRole.Surface, false);
            var toggle = root.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = track;
            toggle.isOn = true;
            var kit = root.AddComponent<KitSwitch>();
            kit.Init(track, slot, knob, shadow);
            return kit;
        }

        // Values 0..1
        public static KitSlider Slider(Transform parent, string name)
        {
            var root = Node(name, parent);
            Size(root, SliderSize);
            var rail = Layer("Rail", root, ThemeRole.Control, true);
            Band(rail.rectTransform, 0f, 0f, SliderRail);
            var fillArea = (RectTransform)Node("Fill Area", root.transform).transform;
            Band(fillArea, SliderHandle / 2f, SliderHandle / 2f, SliderRail);
            var fill = Layer("Fill", fillArea.gameObject, ThemeRole.Primary, false);
            // The fill reaches back to the rail's start and ends under the handle's centre
            fill.rectTransform.offsetMin = new Vector2(-SliderHandle / 2f, 0f);
            fill.rectTransform.offsetMax = Vector2.zero;
            var slideArea = (RectTransform)Node("Handle Slide Area", root.transform).transform;
            Stretch(slideArea.gameObject);
            slideArea.offsetMin = new Vector2(SliderHandle / 2f, 0f);
            slideArea.offsetMax = new Vector2(-SliderHandle / 2f, 0f);
            var handle = (RectTransform)Node("Handle", slideArea.gameObject.transform).transform;
            handle.sizeDelta = new Vector2(SliderHandle, SliderHandle - SliderSize.y);
            var shadow = Layer("Shadow", handle.gameObject, ThemeRole.Scrim, false);
            var knob = Layer("Knob", handle.gameObject, ThemeRole.Primary, false);
            var dot = Layer("Dot", knob.gameObject, ThemeRole.OnPrimary, false);
            Size(dot.gameObject, new Vector2(22f, 22f));
            var slider = root.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.transition = Selectable.Transition.None;
            var kit = root.AddComponent<KitSlider>();
            kit.Init(rail, fill, knob, dot, shadow);
            return kit;
        }

        public static KitSegmented Segmented(Transform parent, string name, int count)
        {
            if (count < 2) throw new System.ArgumentOutOfRangeException(nameof(count), count, "a segmented picker needs two options or more");
            var root = Node(name, parent);
            Size(root, new Vector2(800f, 96f));
            var track = Layer("Track", root, ThemeRole.Track, false);
            var indicator = Layer("Indicator", root, ThemeRole.Primary, false);
            indicator.rectTransform.offsetMin = new Vector2(6f, 6f);
            indicator.rectTransform.offsetMax = new Vector2(-6f, -6f);
            var options = new Button[count];
            var labels = new TMP_Text[count];
            for (var i = 0; i < count; i++)
            {
                // A clear hit area per option
                var hit = Layer($"Option{i}", root, ThemeRole.Picture, true);
                hit.GetComponent<ThemedGraphic>().SetTone(0f, 0f);
                var rect = hit.rectTransform;
                rect.anchorMin = new Vector2((float)i / count, 0f);
                rect.anchorMax = new Vector2((float)(i + 1) / count, 1f);
                var label = Text("Label", hit.gameObject, i == 0 ? ThemeRole.OnPrimary : ThemeRole.Text, TextRole.Title, 40f);
                label.enableAutoSizing = true;
                label.fontSizeMax = 40f;
                label.fontSizeMin = 24f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.margin = new Vector4(12f, 0f, 12f, 0f);
                var button = hit.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                hit.gameObject.AddComponent<PressFeedback>();
                options[i] = button;
                labels[i] = label;
            }
            var kit = root.AddComponent<KitSegmented>();
            kit.Init(track, indicator, options, labels);
            return kit;
        }

        public static KitBadge Badge(Transform parent, string name, KitBadgeTone tone)
        {
            var root = Node(name, parent);
            Size(root, new Vector2(120f, 56f));
            var body = Layer("Body", root, tone == KitBadgeTone.Accent ? ThemeRole.Accent : ThemeRole.Primary, false);
            var label = Text("Label", root, tone == KitBadgeTone.Accent ? ThemeRole.OnAccent : ThemeRole.OnPrimary, TextRole.Title, 32f);
            label.enableAutoSizing = true;
            label.fontSizeMax = 32f;
            label.fontSizeMin = 20f;
            label.margin = new Vector4(14f, 0f, 14f, 0f);
            var badge = root.AddComponent<KitBadge>();
            badge.Init(tone, body, label);
            return badge;
        }

        public static CountUpText CountUp(Transform parent, string name, TextRole role, float size)
        {
            var text = Text(name, parent.gameObject, ThemeRole.Text, role, size);
            Size(text.gameObject, new Vector2(600f, size * 1.4f));
            var count = text.gameObject.AddComponent<CountUpText>();
            count.SetValue(0);
            return count;
        }

        // A full-size layer for the burst; popTarget (may be null) pops with it
        public static Celebration CelebrationLayer(Transform parent, string name, RectTransform popTarget)
        {
            var root = Node(name, parent);
            Stretch(root);
            var celebration = root.AddComponent<Celebration>();
            celebration.Init(popTarget);
            return celebration;
        }

        // Normal and selected untinted, pressed a touch darker, disabled faded; the fade runs on unscaled time
        public static ColorBlock PressColours()
        {
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = Color.white;
            colours.highlightedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colours.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colours.fadeDuration = 0.08f;
            return colours;
        }

        private static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image Layer(string name, GameObject parent, ThemeRole role, bool raycast)
        {
            var go = Node(name, parent.transform);
            Stretch(go);
            var image = go.AddComponent<Image>();
            image.raycastTarget = raycast;
            go.AddComponent<ThemedGraphic>().SetRole(role);
            return image;
        }

        private static TextMeshProUGUI Text(string name, GameObject parent, ThemeRole colour, TextRole font, float size)
        {
            var go = Node(name, parent.transform);
            Stretch(go);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            go.AddComponent<ThemedGraphic>().SetRole(colour);
            go.AddComponent<ThemedText>().SetRole(font);
            return text;
        }

        private static void Size(GameObject go, Vector2 size)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Across the parent less left and right units, height units tall around its middle
        private static void Band(RectTransform rect, float left, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(left, -height / 2f);
            rect.offsetMax = new Vector2(-right, height / 2f);
        }
    }
}
