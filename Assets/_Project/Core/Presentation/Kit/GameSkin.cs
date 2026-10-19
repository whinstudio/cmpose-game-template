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
    // and the display font. Every section is optional; a missing file or broken JSON is no skin, a sprite that does
    // not load drops the whole ui section (the kit's own shapes, as without a skin), a font that does not load drops
    // only the font. The icon button entry is optional too (a pack without a round button): icon buttons keep the
    // kit's shape.
    public sealed class GameSkin
    {
        public const string ResourcePath = "GameSkin";

        private static GameSkin current;
        private static bool loaded;

        private readonly Dictionary<KitButtonVariant, SkinButton> buttons = new Dictionary<KitButtonVariant, SkinButton>();
        private TMP_FontAsset displayFont;

        public Color? BrandBackground { get; private set; }
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

        // The sprites and label colour of a button variant; null without a ui section
        public SkinButton Button(KitButtonVariant variant) => buttons.TryGetValue(variant, out var button) ? button : null;

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
            if (Section(root, "brand") is Dictionary<string, object> brand && TryColour(Text(brand, "background"), out var background))
                skin.BrandBackground = background;
            if (Section(root, "ui") is Dictionary<string, object> ui)
            {
                if (!skin.ReadUi(ui, sprite)) skin.ClearUi();
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
        private bool ReadUi(Dictionary<string, object> ui, Func<string, Sprite> sprite)
        {
            if (ui.TryGetValue("sliceScale", out var scale))
            {
                if (!(scale is double value) || value <= 0d || double.IsNaN(value) || double.IsInfinity(value)) return false;
                SliceScale = (float)value;
            }
            Pixel = ui.TryGetValue("pixel", out var pixel) && pixel is bool on && on;
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
            SliceScale = 1f;
            Pixel = false;
        }

        private static Sprite Load(Func<string, Sprite> sprite, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var found = sprite(path);
            return found != null ? found : null;
        }

        private static object Section(Dictionary<string, object> parent, string key) => parent.TryGetValue(key, out var value) ? value : null;

        private static string Text(Dictionary<string, object> parent, string key) => Section(parent, key) as string;

        // "#RRGGBB" only
        private static bool TryColour(string text, out Color colour)
        {
            colour = Color.white;
            if (text == null || text.Length != 7 || text[0] != '#') return false;
            if (!int.TryParse(text.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return false;
            colour = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
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
    }
}
