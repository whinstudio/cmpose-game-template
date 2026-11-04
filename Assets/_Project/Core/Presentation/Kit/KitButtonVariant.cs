namespace Kturker.UnityBase.Core.Presentation
{
    // Serialized as ints: a new variant goes at the end, the values before it stay
    public enum KitButtonVariant
    {
        // The screen's main action: Primary with OnPrimary text
        Primary,
        // A quieter action: Surface with Text and a Control outline
        Secondary,
        // A round icon or one-glyph button (pause, settings, back): a Primary circle
        Icon,
        // The big "Play": Accent with OnAccent text, rounder, breathing while idle (IdlePulse)
        Play,
        // A destructive action (leaving a run loses it): Danger with OnPrimary text, shaped like Primary
        Danger,
        // A card's close: a round red X, shaped like Icon, Danger with an OnPrimary glyph
        Close
    }
}
