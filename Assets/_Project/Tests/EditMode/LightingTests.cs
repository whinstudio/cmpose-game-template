using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    // A mobile URP game is lit by GameRig's lights and ThemeRig's ambient from code: no build scene runs Enlighten
    // (realtime GI) or bakes, and none regenerates its lighting on its own (Enlighten's editor worker crashed batchmode
    // test runs). Read from the scene files, so the check opens no scene.
    public sealed class LightingTests
    {
        private static readonly Regex LightingSettingsRef = new Regex(@"m_LightingSettings: \{fileID: -?\d+, guid: ([0-9a-f]{32})");

        [Test]
        public void Every_build_scene_has_realtime_gi_baking_and_bake_on_load_off()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            Assert.That(scenes, Is.Not.Empty, "build scenes");
            foreach (var path in scenes)
            {
                var text = File.ReadAllText(path);
                Assert.That(text, Does.Contain("m_EnableRealtimeLightmaps: 0"), $"{path}: realtime GI in the scene's GI settings");
                // Unity 6's automatic generation: baking when the scene loads (BakeOnSceneLoadMode.Never is 0)
                Assert.That(text, Does.Contain("m_BakeOnSceneLoad: 0"), $"{path}: bakes on scene load");
                var match = LightingSettingsRef.Match(text);
                Assert.That(match.Success, Is.True, $"{path}: no Lighting Settings asset (the editor's default generates lighting)");
                var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
                Assert.That(settings, Is.Not.Null, $"{path}: its Lighting Settings asset is missing");
                Assert.That(settings.realtimeGI, Is.False, $"{path}: realtime GI");
                Assert.That(settings.bakedGI, Is.False, $"{path}: baked GI");
            }
        }
    }
}
