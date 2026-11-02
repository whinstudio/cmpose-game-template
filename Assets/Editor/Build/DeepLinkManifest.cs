using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEditor.Build;

namespace Kturker.UnityBase.Editor.Build
{
    // Edits the generated unityLibrary manifest: a VIEW intent-filter for the deep link scheme on every launcher
    // activity, and the VIBRATE permission the haptics module needs. Applying it twice changes nothing.
    public static class DeepLinkManifest
    {
        public const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string VibratePermission = "android.permission.VIBRATE";

        public static void Apply(XmlDocument manifest, string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new BuildFailedException("Deep link scheme is empty: set Player Settings > iOS > Supported URL schemes, element 0");
            var root = manifest.DocumentElement;
            if (root == null || root.Name != "manifest")
                throw new BuildFailedException("AndroidManifest.xml has no <manifest> root");
            var launchers = Elements(root, "application").SelectMany(a => Elements(a, "activity")).Where(IsLauncher).ToList();
            if (launchers.Count == 0)
                throw new BuildFailedException("AndroidManifest.xml has no launcher activity to receive deep links");
            foreach (var activity in launchers)
            {
                if (!HasSchemeFilter(activity, scheme)) activity.AppendChild(SchemeFilter(manifest, scheme));
            }
            if (!Elements(root, "uses-permission").Any(p => p.GetAttribute("name", AndroidNs) == VibratePermission))
                root.PrependChild(Element(manifest, "uses-permission", "name", VibratePermission));
        }

        // Sets android:launchMode on every launcher activity (the purchases flag needs singleTop: with Unity's
        // singleTask, leaving the app during a Google Play purchase can cancel it). Applying it twice changes nothing.
        public static void SetLaunchMode(XmlDocument manifest, string mode)
        {
            var root = manifest.DocumentElement;
            if (root == null || root.Name != "manifest")
                throw new BuildFailedException("AndroidManifest.xml has no <manifest> root");
            var launchers = Elements(root, "application").SelectMany(a => Elements(a, "activity")).Where(IsLauncher).ToList();
            if (launchers.Count == 0)
                throw new BuildFailedException("AndroidManifest.xml has no launcher activity to set the launch mode on");
            foreach (var activity in launchers)
            {
                var attr = activity.GetAttributeNode("launchMode", AndroidNs);
                if (attr == null)
                {
                    attr = manifest.CreateAttribute("android", "launchMode", AndroidNs);
                    activity.Attributes.Append(attr);
                }
                attr.Value = mode;
            }
        }

        // Sets android:enableOnBackInvokedCallback="false" on every launcher activity. Targeting API 36, Android 16
        // turns predictive back on and stops sending KEYCODE_BACK, so Unity never sees Escape and the back key or
        // gesture does nothing in the game. Applying it twice changes nothing.
        public static void UseKeyBack(XmlDocument manifest)
        {
            var root = manifest.DocumentElement;
            if (root == null || root.Name != "manifest")
                throw new BuildFailedException("AndroidManifest.xml has no <manifest> root");
            var launchers = Elements(root, "application").SelectMany(a => Elements(a, "activity")).Where(IsLauncher).ToList();
            if (launchers.Count == 0)
                throw new BuildFailedException("AndroidManifest.xml has no launcher activity to set the back handling on");
            foreach (var activity in launchers)
            {
                var attr = activity.GetAttributeNode("enableOnBackInvokedCallback", AndroidNs);
                if (attr == null)
                {
                    attr = manifest.CreateAttribute("android", "enableOnBackInvokedCallback", AndroidNs);
                    activity.Attributes.Append(attr);
                }
                attr.Value = "false";
            }
        }

        private static IEnumerable<XmlElement> Elements(XmlElement parent, string name) =>
            parent.ChildNodes.OfType<XmlElement>().Where(e => e.Name == name);

        private static bool HasChild(XmlElement parent, string name, string value) =>
            Elements(parent, name).Any(e => e.GetAttribute("name", AndroidNs) == value);

        private static bool IsLauncher(XmlElement activity) =>
            Elements(activity, "intent-filter").Any(f =>
                HasChild(f, "action", "android.intent.action.MAIN") && HasChild(f, "category", "android.intent.category.LAUNCHER"));

        private static bool HasSchemeFilter(XmlElement activity, string scheme) =>
            Elements(activity, "intent-filter").Any(f =>
                HasChild(f, "action", "android.intent.action.VIEW") && Elements(f, "data").Any(d => d.GetAttribute("scheme", AndroidNs) == scheme));

        private static XmlElement SchemeFilter(XmlDocument doc, string scheme)
        {
            var filter = doc.CreateElement("intent-filter");
            filter.AppendChild(Element(doc, "action", "name", "android.intent.action.VIEW"));
            filter.AppendChild(Element(doc, "category", "name", "android.intent.category.DEFAULT"));
            filter.AppendChild(Element(doc, "category", "name", "android.intent.category.BROWSABLE"));
            filter.AppendChild(Element(doc, "data", "scheme", scheme));
            return filter;
        }

        // Explicit 'android' prefix: SetAttribute(name, ns, value) may invent a prefix when the document is saved
        private static XmlElement Element(XmlDocument doc, string name, string attribute, string value)
        {
            var element = doc.CreateElement(name);
            var attr = doc.CreateAttribute("android", attribute, AndroidNs);
            attr.Value = value;
            element.Attributes.Append(attr);
            return element;
        }
    }
}
