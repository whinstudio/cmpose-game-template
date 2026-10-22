using System;
using System.Collections.Generic;
using System.Globalization;
using Kturker.UnityBase.Core.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Kturker.UnityBase.Core.Presentation
{
    // The game's dress, written by Compass to Resources/GameSkin.json: the brand colour and, for a pixel 2D game, the
    // Kenney UI sprites the kit draws its buttons, cards and sheets with (in their own colours, not the palette's)
    // and the display font. Every section is optional; a missing file, broken JSON or a version other than 1 or 2
    // (none reads as 1) is no skin, a font that does not load drops only the font. The icon button entry is optional
    // too (a pack without a round button): icon buttons keep the kit's shape.
    // Version 2 adds the palette, backgrounds, font ids and the ui parts pill, slider, switch, progress, star, icons
    // and digits. A sprite of the version 1 parts (buttons, card, sheet, dialog) that does not load drops those four
    // together (the kit's own shapes, as without a skin); every version 2 part fails on its own: each palette key,
    // background, the scrim and each icon alone, a pill, slider, switch or progress as a whole when a sprite it names
    // does not load (a key left out is fine), a star without both sprites, digits without all ten. A bad sliceScale
    // drops every ui sprite (version 1 and 2); the palette, backgrounds and fonts stay.
    public sealed class GameSkin
    {
        public const string ResourcePath = "GameSkin";

        private static GameSkin current;
        private static bool loaded;

        private readonly Dictionary<KitButtonVariant, SkinButton> buttons = new Dictionary<KitButtonVariant, SkinButton>();
        private readonly Dictionary<ThemeRole, Color> palette = new Dictionary<ThemeRole, Color>();
        private readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private TMP_FontAsset displayFont;

        public int Version { get; private set; } = 1;
        public Color? BrandBackground { get; private set; }
        // Role colours that replace the theme's, light and dark alike; empty without a palette section
        public IReadOnlyDictionary<ThemeRole, Color> Palette => palette;
        public Sprite MenuBackground { get; private set; }
        public Sprite GameBackground { get; private set; }
        // Drawn over a background so the text on it reads
        public Color? Scrim { get; private set; }
        // Template font ids (the font catalogue resolves them); null without a fonts entry
        public string DisplayFontId { get; private set; }
        public string BodyFontId { get; private set; }
        public SkinPill Pill { get; private set; }
        public SkinSlider Slider { get; private set; }
        public SkinSwitch Switch { get; private set; }
        public SkinProgress Progress { get; private set; }
        public SkinStar Star { get; private set; }
        // hud0..hud9 in order; null unless all ten load
        public Sprite[] Digits { get; private set; }
        // Panel sprites (null without a ui section)
        public Sprite Card { get; private set; }
        public Sprite Sheet { get; private set; }
        public Sprite Dialog { get; private set; }
        // Source pixels per reference unit: a 48 px sprite drawn at SliceScale 4 has 4-unit pixels
        public float SliceScale { get; private set; } = 1f;
        // A pixel pack: drawn tiled so its edge patterns repeat instead of stretching (a soft pack is sliced)
        public bool Pixel { get; private set; }
        public Font FontSource { get; private set; }

        // Null when the file is absent or broken; read once
        public static GameSkin Current
        {
            get
            {
                if (!loaded)
                {
                    current = Read(Resources.Load<TextAsset>(ResourcePath));
                    loaded = true;
                }
                return current;
            }
        }

        // A dynamic SDF font made from FontSource the first time a title asks, falling back to the theme's display
        // font; null without a font
        public TMP_FontAsset DisplayFont
        {
            get
            {
                if (displayFont != null || FontSource == null) return displayFont;
                displayFont = TMP_FontAsset.CreateFontAsset(FontSource, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                if (displayFont == null) return null;
                displayFont.name = $"{FontSource.name} (skin)";
                var style = Resources.Load<ThemeStyle>(ThemeStyle.ResourcePath);
                if (style != null && style.DisplayFont != null)
                    displayFont.fallbackFontAssetTable = new List<TMP_FontAsset> { style.DisplayFont };
                return displayFont;
            }
        }

        // The skin's font for a text role: titles take the display font id, else the version 1 font; body and captions
        // the body font id. Null when the skin names none that loads (the theme's font stays)
        public TMP_FontAsset Font(TextRole role)
        {
            if (role != TextRole.Display && role != TextRole.Title) return SkinFonts.Load(BodyFontId);
            var display = SkinFonts.Load(DisplayFontId);
            return display != null ? display : DisplayFont;
        }

        // The sprites and label colour of a button variant; null without a ui section
        public SkinButton Button(KitButtonVariant variant) => buttons.TryGetValue(variant, out var button) ? button : null;

        // The sprite of an icon role ("coin", "heart", "gem", "key"); null when the skin has none
        public Sprite Icon(string role) => role != null && icons.TryGetValue(role, out var icon) ? icon : null;

        // Replaces Current (null: read the file again)
        internal static void ForTests(GameSkin skin)
        {
            current = skin;
            loaded = skin != null;
        }

        internal static GameSkin Read(TextAsset file) =>
            file == null ? null : Parse(file.text, path => Resources.Load<Sprite>(path), path => Resources.Load<Font>(path));

        // sprite and font load a Resources path (no extension) and return null when there is nothing there
        public static GameSkin Parse(string json, Func<string, Sprite> sprite, Func<string, Font> font)
        {
            if (!JsonText.TryParse(json, out var parsed) || !(parsed is Dictionary<string, object> root)) return null;
            var skin = new GameSkin();
            if (root.TryGetValue("version", out var version))
            {
                if (!(version is double number) || (number != 1d && number != 2d)) return null;
                skin.Version = (int)number;
            }
            if (Section(root, "brand") is Dictionary<string, object> brand && TryColour(Text(brand, "background"), out var background))
                skin.BrandBackground = background;
            if (Section(root, "palette") is Dictionary<string, object> roles) skin.ReadPalette(roles);
            if (Section(root, "backgrounds") is Dictionary<string, object> backgrounds)
            {
                skin.MenuBackground = Load(sprite, Text(backgrounds, "menu"));
                skin.GameBackground = Load(sprite, Text(backgrounds, "game"));
                if (TryColour(Text(backgrounds, "scrim"), out var scrim)) skin.Scrim = scrim;
            }
            if (Section(root, "fonts") is Dictionary<string, object> fonts)
            {
                skin.DisplayFontId = NonEmpty(Text(fonts, "display"));
                skin.BodyFontId = NonEmpty(Text(fonts, "body"));
            }
            if (Section(root, "ui") is Dictionary<string, object> ui)
            {
                var scaled = skin.ReadScale(ui);
                if (!scaled || !skin.ReadUi(ui, sprite)) skin.ClearUi();
                skin.ReadParts(ui, scaled ? sprite : _ => null);
                var fontPath = Text(ui, "font");
                if (fontPath != null) skin.FontSource = font(fontPath);
            }
            return skin;
        }

        private static readonly (KitButtonVariant Variant, string Key)[] ButtonKeys =
        {
            (KitButtonVariant.Primary, "primary"),
            (KitButtonVariant.Secondary, "secondary"),
            (KitButtonVariant.Play, "play"),
            (KitButtonVariant.Icon, "icon")
        };

        // All or nothing: a half-dressed screen reads worse than the kit's own shapes. Only the icon entry may be left
        // out (Compass writes none when the pack has no round button); one that is there but broken drops the section
        // like any other
        // False when sliceScale is bad: no ui sprite can be drawn at a scale the file does not give
        private bool ReadScale(Dictionary<string, object> ui)
        {
            if (ui.TryGetValue("sliceScale", out var scale))
            {
                if (!(scale is double value) || value <= 0d || double.IsNaN(value) || double.IsInfinity(value)) return false;
                SliceScale = (float)value;
            }
            Pixel = ui.TryGetValue("pixel", out var pixel) && pixel is bool on && on;
            return true;
        }

        private bool ReadUi(Dictionary<string, object> ui, Func<string, Sprite> sprite)
        {
            if (!(Section(ui, "buttons") is Dictionary<string, object> entries)) return false;
            foreach (var (variant, key) in ButtonKeys)
            {
                if (variant == KitButtonVariant.Icon && !entries.ContainsKey(key)) continue;
                if (!(Section(entries, key) is Dictionary<string, object> entry)) return false;
                var normal = Load(sprite, Text(entry, "normal"));
                var pressed = Load(sprite, Text(entry, "pressed"));
                if (normal == null || pressed == null || !TryColour(Text(entry, "text"), out var text)) return false;
                buttons[variant] = new SkinButton(normal, pressed, text);
            }
            Card = Load(sprite, Text(ui, "card"));
            Sheet = Load(sprite, Text(ui, "sheet"));
            Dialog = Load(sprite, Text(ui, "dialog"));
            return Card != null && Sheet != null && Dialog != null;
        }

        private void ClearUi()
        {
            buttons.Clear();
            Card = null;
            Sheet = null;
            Dialog = null;
        }

        private void ReadPalette(Dictionary<string, object> roles)
        {
            foreach (var pair in roles)
            {
                // IsDefined: the exact name only, not a role's number
                if (!Enum.IsDefined(typeof(ThemeRole), pair.Key) || !Enum.TryParse(pair.Key, false, out ThemeRole role)) continue;
                if (TryColour(pair.Value as string, out var colour)) palette[role] = colour;
            }
        }

        // The version 2 ui parts, each on its own; after a bad sliceScale sprite loads nothing, so every part that
        // names a sprite drops
        private void ReadParts(Dictionary<string, object> ui, Func<string, Sprite> sprite)
        {
            if (Section(ui, "pill") is Dictionary<string, object> pill && TryPart(pill, sprite, PillKeys, out var pillSprite)
                && TryLabel(Text(pill, "text"), out var label) && (pillSprite[0] != null || label != null))
                Pill = new SkinPill(pillSprite[0], label);
            if (Section(ui, "slider") is Dictionary<string, object> slider && TryPart(slider, sprite, SliderKeys, out var rail) && Any(rail))
                Slider = new SkinSlider(rail[0], rail[1], rail[2]);
            if (Section(ui, "switch") is Dictionary<string, object> toggle && TryPart(toggle, sprite, SwitchKeys, out var lever) && Any(lever))
                Switch = new SkinSwitch(lever[0], lever[1]);
            if (Section(ui, "progress") is Dictionary<string, object> progress && TryPart(progress, sprite, ProgressKeys, out var bar) && Any(bar))
                Progress = new SkinProgress(bar[0], bar[1]);
            if (Section(ui, "star") is Dictionary<string, object> star)
            {
                var full = Load(sprite, Text(star, "full"));
                var empty = Load(sprite, Text(star, "empty"));
                if (full != null && empty != null) Star = new SkinStar(full, empty);
            }
            if (Section(ui, "icons") is Dictionary<string, object> entries)
            {
                foreach (var pair in entries)
                {
                    var icon = Load(sprite, pair.Value as string);
                    if (icon != null) icons[pair.Key] = icon;
                }
            }
            Digits = ReadDigits(Text(ui, "digits"), sprite);
        }

        private static readonly string[] PillKeys = { "sprite" };
        private static readonly string[] SliderKeys = { "track", "fill", "knob" };
        private static readonly string[] SwitchKeys = { "track", "knob" };
        private static readonly string[] ProgressKeys = { "track", "fill" };

        // The sprites of a part in key order; false when a key the part gives does not load (a key left out is null)
        private static bool TryPart(Dictionary<string, object> part, Func<string, Sprite> sprite, string[] keys, out Sprite[] loaded)
        {
            loaded = new Sprite[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                if (!part.ContainsKey(keys[i])) continue;
                if ((loaded[i] = Load(sprite, Text(part, keys[i]))) == null) return false;
            }
            return true;
        }

        private static bool Any(Sprite[] sprites) => Array.Exists(sprites, part => part != null);

        // A label colour left out is null; one that is there but bad fails
        private static bool TryLabel(string text, out Color? colour)
        {
            colour = null;
            if (text == null) return true;
            if (!TryColour(text, out var parsed)) return false;
            colour = parsed;
            return true;
        }

        // <prefix>0 .. <prefix>9, all or nothing
        private static Sprite[] ReadDigits(string prefix, Func<string, Sprite> sprite)
        {
            if (string.IsNullOrEmpty(prefix)) return null;
            var digits = new Sprite[10];
            for (var i = 0; i < digits.Length; i++)
                if ((digits[i] = Load(sprite, prefix + i.ToString(CultureInfo.InvariantCulture))) == null) return null;
            return digits;
        }

        private static string NonEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;

        private static Sprite Load(Func<string, Sprite> sprite, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var found = sprite(path);
            return found != null ? found : null;
        }

        private static object Section(Dictionary<string, object> parent, string key) => parent.TryGetValue(key, out var value) ? value : null;

        private static string Text(Dictionary<string, object> parent, string key) => Section(parent, key) as string;

        // "#RRGGBB" or "#RRGGBBAA"
        private static bool TryColour(string text, out Color colour)
        {
            colour = Color.white;
            if (text == null || (text.Length != 7 && text.Length != 9) || text[0] != '#') return false;
            if (!uint.TryParse(text.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)) return false;
            var rgba = text.Length == 7 ? (value << 8) | 0xFF : value;
            colour = new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
            return true;
        }
    }

    public sealed class SkinButton
    {
        public SkinButton(Sprite normal, Sprite pressed, Color text)
        {
            Normal = normal;
            Pressed = pressed;
            Text = text;
        }

        public Sprite Normal { get; }
        // Drawn while the finger is down
        public Sprite Pressed { get; }
        // The label's and the glyph's colour
        public Color Text { get; }
        // How far the normal sprite's face stands above the pressed one's, in source pixels (0: a flat pack)
        public float Depth => Normal != null && Pressed != null ? Mathf.Max(0f, Normal.rect.height - Pressed.rect.height) : 0f;
    }
}
