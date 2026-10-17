namespace Kturker.UnityBase.Core.Presentation
{
    // Easing curves of UiMotion. Every curve starts at 0 and ends at exactly 1; OutBack and Spring pass 1 on the way.
    public enum Ease
    {
        Linear,
        OutCubic,
        InOutCubic,
        OutQuint,
        OutBack,
        // A damped spring: overshoots, settles back, ends at rest
        Spring
    }
}
