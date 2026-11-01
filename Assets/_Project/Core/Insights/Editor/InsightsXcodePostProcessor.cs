using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights.Editor
{
    // iOS side of the insights flag: analytics collection off at launch in Info.plist (the bridge turns it on after
    // consent). When Assets/Firebase/GoogleService-Info.plist exists, it is copied into the app and the Crashlytics
    // symbol upload is added to the app target; no file: Firebase never starts. The Pods (InsightsDependencies.xml)
    // are installed by EDM4U into UnityFramework, before this step (order 50).
    public sealed class InsightsXcodePostProcessor : IPostprocessBuildWithReport
    {
        public const string ConfigPath = "Assets/Firebase/GoogleService-Info.plist";
        private const string ConfigName = "GoogleService-Info.plist";
        private const string UploadPhase = "Upload Crashlytics symbols";

        // The app target has no Pods of its own, so PODS_ROOT may be unset there; EDM4U's pod install puts Pods/ next
        // to the Xcode project. Only builds that make dSYMs (Release, Archive) and still have the config upload; the app's
        // GoogleService-Info.plist is found in the built app.
        private const string UploadScript =
            "[ \"${DEBUG_INFORMATION_FORMAT}\" = \"dwarf-with-dsym\" ] || exit 0\n" +
            "[ -f \"${PROJECT_DIR}/GoogleService-Info.plist\" ] || exit 0\n" +
            "\"${PODS_ROOT:-${PROJECT_DIR}/Pods}/FirebaseCrashlytics/run\"\n";

        public int callbackOrder => 110;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var outputPath = report.summary.outputPath;

            var plistPath = Path.Combine(outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            plist.WriteToFile(plistPath);

            var projectPath = PBXProject.GetPBXProjectPath(outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var mainTarget = project.GetUnityMainTargetGuid();

            if (!File.Exists(ConfigPath))
            {
                // an appended Xcode project keeps the files of earlier builds: drop a config removed since then
                var stale = project.FindFileGuidByProjectPath(ConfigName);
                if (stale != null)
                {
                    project.RemoveFile(stale);
                    project.WriteToFile(projectPath);
                }
                File.Delete(Path.Combine(outputPath, ConfigName));
                Debug.Log("CMPOSE_INSIGHTS_IOS config=none");
                return;
            }

            File.Copy(ConfigPath, Path.Combine(outputPath, ConfigName), true);
            if (project.FindFileGuidByProjectPath(ConfigName) == null)
                project.AddFileToBuild(mainTarget, project.AddFile(ConfigName, ConfigName));

            if (!HasPhase(project, mainTarget, UploadPhase))
                project.AddShellScriptBuildPhase(mainTarget, UploadPhase, "/bin/sh", UploadScript);

            foreach (var target in new[] { mainTarget, project.GetUnityFrameworkTargetGuid() })
                project.SetBuildPropertyForConfig(project.BuildConfigByName(target, "Release"), "DEBUG_INFORMATION_FORMAT",
                    "dwarf-with-dsym");

            project.WriteToFile(projectPath);
            Debug.Log("CMPOSE_INSIGHTS_IOS config=GoogleService-Info.plist");
        }

        // Append builds run this again on the same project
        private static bool HasPhase(PBXProject project, string target, string name)
        {
            foreach (var phase in project.GetAllBuildPhasesForTarget(target))
                if (project.GetBuildPhaseName(phase) == name) return true;
            return false;
        }
    }
}
