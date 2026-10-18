using System.IO;
using UnityEngine;

namespace Kturker.UnityBase.Core.Ads
{
    // A Compass capture run (screenshots, autoplay video): Compass writes the empty flag file into persistentDataPath
    // before it launches the app, the same name its kit's CompassAutoPlay reads. Real players never have it.
    public static class AutomationRun
    {
        public const string FlagFile = "compass-autoplay";

        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Detect() =>
            Active = File.Exists(Path.Combine(Application.persistentDataPath, FlagFile));
    }
}
