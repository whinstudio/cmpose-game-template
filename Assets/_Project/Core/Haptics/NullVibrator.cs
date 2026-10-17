namespace Kturker.UnityBase.Core.Haptics
{
    // Editor and platforms without a vibrator
    public sealed class NullVibrator : IVibrator
    {
        public void Vibrate(HapticStrength strength)
        {
        }

        public void Vibrate(HapticEvent e)
        {
        }
    }
}
