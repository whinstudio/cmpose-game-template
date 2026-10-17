#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using Kturker.UnityBase.Core.Domain;
using UnityEngine;

namespace Kturker.UnityBase.Core.Haptics
{
    // android.os.Vibrator: VibrationEffect.createOneShot / createWaveform from API 26, the plain duration and pattern
    // calls on API 25.
    // Needs android.permission.VIBRATE, which the manifest postprocessor adds. Turns itself off after an error.
    public sealed class AndroidVibrator : IVibrator
    {
        private readonly ILog log;
        private AndroidJavaObject vibrator;
        private int sdk;
        private bool failed;

        public AndroidVibrator(ILog log) => this.log = log;

        public void Vibrate(HapticStrength strength)
        {
            if (failed) return;
            try
            {
                if (vibrator == null) Connect();
                var duration = HapticPattern.DurationMs(strength);
                if (sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", duration, HapticPattern.Amplitude(strength));
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", duration);
                }
            }
            catch (Exception e)
            {
                failed = true;
                log.Warning($"Haptics turned off: {e.Message}");
            }
        }

        public void Vibrate(HapticEvent e)
        {
            if (failed) return;
            try
            {
                if (vibrator == null) Connect();
                var timings = HapticPattern.Timings(e);
                if (sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createWaveform", timings, HapticPattern.Amplitudes(e), -1);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", timings, -1);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                log.Warning($"Haptics turned off: {ex.Message}");
            }
        }

        private void Connect()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (vibrator == null) throw new InvalidOperationException("no vibrator service");
        }
    }
}
#endif
