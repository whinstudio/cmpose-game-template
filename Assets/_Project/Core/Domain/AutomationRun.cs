using System.IO;

namespace Kturker.UnityBase.Core.Domain
{
    // A Compass capture run: Compass writes an empty flag file into persistentDataPath before it launches the app.
    // `compass-autoplay` (autoplay video and store shots, the name its kit's CompassAutoPlay reads) or
    // `compass-capture` (the menu store shot, which must not start the AutoPlayer). Real players never have either.
    // Unity-free like the rest of Domain: Core/DI's AutomationRunStartup calls Detect with persistentDataPath.
    public static class AutomationRun
    {
        public const string FlagFile = "compass-autoplay";
        public const string CaptureFlagFile = "compass-capture";

        public static bool Active { get; private set; }

        public static bool IsActiveIn(string directory) =>
            File.Exists(Path.Combine(directory, FlagFile)) || File.Exists(Path.Combine(directory, CaptureFlagFile));

        // Start-up only: AutomationRunStartup calls this once; everything else reads Active (or IsActiveIn).
        public static void Detect(string directory) => Active = IsActiveIn(directory);
    }
}
