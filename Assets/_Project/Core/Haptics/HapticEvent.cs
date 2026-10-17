namespace Kturker.UnityBase.Core.Haptics
{
    // Named game events with their own pattern, apart from a single impact. Values are passed to the iOS plugin as
    // ints (CmposeHaptics.mm)
    public enum HapticEvent
    {
        Success = 0,
        Failure = 1
    }
}
