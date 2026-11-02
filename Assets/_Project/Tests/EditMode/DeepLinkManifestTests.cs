using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Kturker.UnityBase.Editor.Build;
using NUnit.Framework;
using UnityEditor.Build;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class DeepLinkManifestTests
    {
        private const string Ns = DeepLinkManifest.AndroidNs;

        // The launcher activity as Unity 6000.3.25f1 writes it into unityLibrary/src/main/AndroidManifest.xml
        private const string Manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" xmlns:tools=""http://schemas.android.com/tools"">
  <uses-permission android:name=""android.permission.INTERNET"" />
  <application android:appCategory=""game"">
    <activity android:exported=""true"" android:launchMode=""singleTask"" android:name=""com.unity3d.player.UnityPlayerGameActivity"">
      <intent-filter>
        <category android:name=""android.intent.category.LAUNCHER"" />
        <action android:name=""android.intent.action.MAIN"" />
      </intent-filter>
      <meta-data android:name=""unityplayer.UnityActivity"" android:value=""true"" />
    </activity>
  </application>
</manifest>";

        private static XmlDocument Load(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        private static List<XmlElement> All(XmlDocument doc, string name) => doc.GetElementsByTagName(name).OfType<XmlElement>().ToList();

        [Test]
        public void Adds_a_view_filter_for_the_scheme_and_the_vibrate_permission()
        {
            var doc = Load(Manifest);

            DeepLinkManifest.Apply(doc, "unitybase");

            var data = All(doc, "data");
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That(data[0].GetAttribute("scheme", Ns), Is.EqualTo("unitybase"));
            var filter = (XmlElement)data[0].ParentNode;
            Assert.That(((XmlElement)filter.ParentNode).GetAttribute("name", Ns), Is.EqualTo("com.unity3d.player.UnityPlayerGameActivity"));
            var names = filter.ChildNodes.OfType<XmlElement>().Select(e => e.GetAttribute("name", Ns)).ToList();
            Assert.That(names, Is.SupersetOf(new[] { "android.intent.action.VIEW", "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }));
            Assert.That(All(doc, "uses-permission").Count(p => p.GetAttribute("name", Ns) == "android.permission.VIBRATE"), Is.EqualTo(1));
            Assert.That(doc.OuterXml, Does.Contain("android:scheme=\"unitybase\""));
        }

        [Test]
        public void Applying_twice_adds_nothing_more()
        {
            var doc = Load(Manifest);
            DeepLinkManifest.Apply(doc, "unitybase");
            var once = doc.OuterXml;

            DeepLinkManifest.Apply(doc, "unitybase");

            Assert.That(doc.OuterXml, Is.EqualTo(once));
        }

        [Test]
        public void An_empty_scheme_or_a_manifest_without_a_launcher_fails_the_build()
        {
            Assert.Throws<BuildFailedException>(() => DeepLinkManifest.Apply(Load(Manifest), ""));
            Assert.Throws<BuildFailedException>(() => DeepLinkManifest.Apply(Load(Manifest), null));
            var noLauncher = Load(Manifest.Replace("android.intent.category.LAUNCHER", "android.intent.category.INFO"));
            Assert.Throws<BuildFailedException>(() => DeepLinkManifest.Apply(noLauncher, "unitybase"));
        }

        [Test]
        public void SetLaunchMode_changes_only_the_launcher_and_twice_is_the_same()
        {
            var doc = Load(Manifest.Replace("</application>",
                "<activity android:name=\"com.example.Other\" android:launchMode=\"singleTask\" /></application>"));

            DeepLinkManifest.SetLaunchMode(doc, "singleTop");
            var once = doc.OuterXml;
            DeepLinkManifest.SetLaunchMode(doc, "singleTop");

            var activities = All(doc, "activity");
            Assert.That(activities.Single(a => a.GetAttribute("name", Ns) == "com.unity3d.player.UnityPlayerGameActivity")
                .GetAttribute("launchMode", Ns), Is.EqualTo("singleTop"));
            Assert.That(activities.Single(a => a.GetAttribute("name", Ns) == "com.example.Other")
                .GetAttribute("launchMode", Ns), Is.EqualTo("singleTask"));
            Assert.That(doc.OuterXml, Is.EqualTo(once));
            Assert.That(doc.OuterXml, Does.Contain("android:launchMode=\"singleTop\""));
        }

        [Test]
        public void UseKeyBack_turns_predictive_back_off_on_the_launcher_and_twice_is_the_same()
        {
            var doc = Load(Manifest.Replace("</application>",
                "<activity android:name=\"com.example.Other\" /></application>"));

            DeepLinkManifest.UseKeyBack(doc);
            var once = doc.OuterXml;
            DeepLinkManifest.UseKeyBack(doc);

            var activities = All(doc, "activity");
            Assert.That(activities.Single(a => a.GetAttribute("name", Ns) == "com.unity3d.player.UnityPlayerGameActivity")
                .GetAttribute("enableOnBackInvokedCallback", Ns), Is.EqualTo("false"));
            Assert.That(activities.Single(a => a.GetAttribute("name", Ns) == "com.example.Other")
                .HasAttribute("enableOnBackInvokedCallback", Ns), Is.False);
            Assert.That(doc.OuterXml, Is.EqualTo(once));
            Assert.That(doc.OuterXml, Does.Contain("android:enableOnBackInvokedCallback=\"false\""));
        }

        [Test]
        public void UseKeyBack_without_a_launcher_fails_the_build()
        {
            var noLauncher = Load(Manifest.Replace("android.intent.category.LAUNCHER", "android.intent.category.INFO"));

            Assert.Throws<BuildFailedException>(() => DeepLinkManifest.UseKeyBack(noLauncher));
        }

        [Test]
        public void SetLaunchMode_without_a_launcher_fails_the_build()
        {
            var noLauncher = Load(Manifest.Replace("android.intent.category.LAUNCHER", "android.intent.category.INFO"));

            Assert.Throws<BuildFailedException>(() => DeepLinkManifest.SetLaunchMode(noLauncher, "singleTop"));
        }
    }
}
