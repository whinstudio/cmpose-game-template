using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The player creates the backbuffer from the quality level's anti-aliasing before URP starts; URP then raises
    // QualitySettings.antiAliasing to its own MSAA, so a lower quality value leaves the first frame rendering 4x MSAA
    // passes into a 1-sample backbuffer (iOS Metal: "RenderPass: Attachment 0 was created with 1 samples but 4 samples
    // were requested"). Every quality level's anti-aliasing must already match its URP asset.
    public sealed class BackbufferMsaaTests
    {
        [Test]
        public void Every_quality_level_anti_aliasing_matches_its_urp_asset_msaa()
        {
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            Assert.That(levels.arraySize, Is.GreaterThan(0), "quality levels");
            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var name = level.FindPropertyRelative("name").stringValue;
                var pipeline = level.FindPropertyRelative("customRenderPipeline").objectReferenceValue as RenderPipelineAsset
                               ?? GraphicsSettings.defaultRenderPipeline;
                Assert.That(pipeline, Is.Not.Null, $"{name}: no render pipeline");
                var msaa = new SerializedObject(pipeline).FindProperty("m_MSAA").intValue;
                // QualitySettings uses 0 for "off" where URP uses 1
                var antiAliasing = Mathf.Max(1, level.FindPropertyRelative("antiAliasing").intValue);
                Assert.That(antiAliasing, Is.EqualTo(msaa), $"{name}: anti-aliasing vs {pipeline.name} MSAA");
            }
        }
    }
}
