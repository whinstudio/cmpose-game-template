using System.Collections.Generic;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy's icons from Resources (the paths in GameEconomy.json, white glyphs the windows tint): a PNG imported
    // as a sprite, or as a plain texture (made into a sprite once). A missing path or file is null: the currency and a
    // booster then show a FallbackIcon's disc with their name's first letter. The currency is drawn in the file's
    // colour (EconomyCurrency.Mark, KitGlyph), the same in every family.
    public static class EconomyIcons
    {
        private static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();

        public static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            if (Made.TryGetValue(path, out sprite) && sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            Made[path] = sprite;
            return sprite;
        }

        // The game's currency icon; null: its fallback disc
        public static Sprite Currency(EconomyConfig config) => config == null ? null : Load(config.Currency.Icon);

        // Null: draw the booster's FallbackIcon
        public static Sprite Booster(BoosterConfig booster) => booster == null ? null : Load(booster.Icon);
    }
}
