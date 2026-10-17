using Kturker.UnityBase.Core.Preferences;

namespace Kturker.UnityBase.Core.Haptics
{
    // Vibrates unless the player turned it off in Settings (haptics.enabled, default on)
    public sealed class HapticsService
    {
        public const string EnabledKey = "haptics.enabled";

        private readonly IPreferences preferences;
        private readonly IVibrator vibrator;

        public HapticsService(IPreferences preferences, IVibrator vibrator)
        {
            this.preferences = preferences;
            this.vibrator = vibrator;
        }

        public bool Enabled
        {
            get => preferences.GetBool(EnabledKey, true);
            set => preferences.SetBool(EnabledKey, value);
        }

        public void Play(HapticStrength strength)
        {
            if (Enabled) vibrator.Vibrate(strength);
        }

        public void Play(HapticEvent e)
        {
            if (Enabled) vibrator.Vibrate(e);
        }
    }
}
