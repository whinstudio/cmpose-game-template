namespace Kturker.UnityBase.Core.Haptics
{
    // The platform's vibration API behind an interface, so HapticsService runs in EditMode tests
    public interface IVibrator
    {
        void Vibrate(HapticStrength strength);
        void Vibrate(HapticEvent e);
    }
}
