#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Kturker.UnityBase.Core.Haptics
{
    // UIImpactFeedbackGenerator and UINotificationFeedbackGenerator through Assets/Plugins/iOS/CmposeHaptics.mm
    // (simulators do not vibrate)
    public sealed class IosVibrator : IVibrator
    {
        [DllImport("__Internal")]
        private static extern void CmposeHapticImpact(int strength);

        [DllImport("__Internal")]
        private static extern void CmposeHapticNotify(int e);

        public void Vibrate(HapticStrength strength) => CmposeHapticImpact((int)strength);

        public void Vibrate(HapticEvent e) => CmposeHapticNotify((int)e);
    }
}
#endif
