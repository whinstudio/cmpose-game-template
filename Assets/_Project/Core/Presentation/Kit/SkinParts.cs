using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The version 2 ui parts of a GameSkin. A null sprite is a part the pack left out: the kit draws that piece in
    // palette colours as without a skin.

    public sealed class SkinPill
    {
        public SkinPill(Sprite sprite, Color? text)
        {
            Sprite = sprite;
            Text = text;
        }

        public Sprite Sprite { get; }
        // The label's colour on the sprite; null keeps the palette's
        public Color? Text { get; }
    }

    public sealed class SkinSlider
    {
        public SkinSlider(Sprite track, Sprite fill, Sprite knob)
        {
            Track = track;
            Fill = fill;
            Knob = knob;
        }

        public Sprite Track { get; }
        public Sprite Fill { get; }
        public Sprite Knob { get; }
    }

    public sealed class SkinSwitch
    {
        public SkinSwitch(Sprite track, Sprite knob)
        {
            Track = track;
            Knob = knob;
        }

        public Sprite Track { get; }
        public Sprite Knob { get; }
    }

    public sealed class SkinProgress
    {
        public SkinProgress(Sprite track, Sprite fill)
        {
            Track = track;
            Fill = fill;
        }

        public Sprite Track { get; }
        public Sprite Fill { get; }
    }

    // Both sprites are always set: a star without its empty side cannot show a score
    public sealed class SkinStar
    {
        public SkinStar(Sprite full, Sprite empty)
        {
            Full = full;
            Empty = empty;
        }

        public Sprite Full { get; }
        public Sprite Empty { get; }
    }
}
