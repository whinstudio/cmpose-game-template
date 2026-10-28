namespace Kturker.UnityBase.Core.Presentation
{
    // What a graphic is, not its colour: the palette decides the colour
    public enum ThemeRole
    {
        Background,
        Surface,
        Primary,
        OnPrimary,
        Text,
        TextMuted,
        Track,
        Scrim,
        // Untinted pictures (avatars, remote images): white in every palette
        Picture,
        // An emphasised box that stands out from Surface and Background in both palettes, and from Track (the GitHub
        // card on the Game screen)
        SurfaceVariant,
        // The rail of a slider and the box of a checkbox: Primary (fill, mark) reads on it and it reads on Surface
        Control,
        // A second call to action that is not Primary (a reward, a shop button); OnAccent is text and icons on it
        Accent,
        OnAccent,
        // Status colours: a win, a gain (Success); a loss, a destructive action (Danger). Danger reads on Surface.
        Success,
        Danger
    }
}
