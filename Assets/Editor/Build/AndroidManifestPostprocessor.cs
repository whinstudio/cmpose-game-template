using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Kturker.UnityBase.Editor.Build
{
    // No custom AndroidManifest.xml is kept: the deep link scheme comes from the single source,
    // PlayerSettings iOSURLSchemes[0], and is injected into the generated unityLibrary manifest.
    public sealed class AndroidManifestPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                throw new BuildFailedException($"AndroidManifest.xml not found in the generated unityLibrary project: {manifestPath}");
            var schemes = PlayerSettings.iOS.iOSUrlSchemes;
            var scheme = schemes != null && schemes.Length > 0 ? schemes[0] : null;
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            DeepLinkManifest.Apply(doc, scheme);
            DeepLinkManifest.UseKeyBack(doc);
            // cmpose:purchases
            DeepLinkManifest.SetLaunchMode(doc, "singleTop");
            // /cmpose:purchases
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (var writer = XmlWriter.Create(manifestPath, settings)) doc.Save(writer);
            Debug.Log($"CMPOSE_ANDROID_MANIFEST scheme={scheme}");
        }
    }
}
