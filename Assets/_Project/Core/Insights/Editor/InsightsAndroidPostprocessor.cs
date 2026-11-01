using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights.Editor
{
    // Android side of the insights flag: when Assets/Firebase/google-services.json exists, writes its resources into
    // the exported copy of CmposeInsights.androidlib (never into Assets/). No file: no resources, so Firebase never
    // starts and insights stay off.
    public sealed class InsightsAndroidPostprocessor : IPostGenerateGradleAndroidProject
    {
        public const string ConfigPath = "Assets/Firebase/google-services.json";
        private const string Library = "CmposeInsights.androidlib";

        public int callbackOrder => 10;

        // path is the exported unityLibrary folder; Unity copies each .androidlib into it
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var library = Path.Combine(path, Library);
            var resources = Path.Combine(library, "src", "main", "res", "values", "cmpose-firebase.xml");
            if (!File.Exists(ConfigPath))
            {
                // the export is reused between builds: a config removed since the last build must not stay in it
                if (File.Exists(resources)) File.Delete(resources);
                Debug.Log("CMPOSE_INSIGHTS_ANDROID config=none");
                return;
            }
            if (!Directory.Exists(library))
                throw new BuildFailedException($"{Library} not found in the exported project: {library}");
            string xml;
            try
            {
                xml = GoogleServicesResources.ToValuesXml(File.ReadAllText(ConfigPath),
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), Guid.NewGuid().ToString("N"));
            }
            catch (InvalidOperationException e)
            {
                throw new BuildFailedException($"{ConfigPath}: {e.Message}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(resources));
            File.WriteAllText(resources, xml, new UTF8Encoding(false));
            Debug.Log("CMPOSE_INSIGHTS_ANDROID config=google-services.json");
        }
    }
}
