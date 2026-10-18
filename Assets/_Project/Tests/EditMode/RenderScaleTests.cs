using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Tests.EditMode
{
    // URP 17.3's 2D Renderer records a post-processing camera's uber pass straight into the backbuffer without making
    // the backbuffer the active target first (Renderer2DRendergraph), so PostProcessPassRenderGraph.ScaleViewport gives
    // it the render-scaled viewport: at render scale 0.8 the world filled the lower-left 80% of the screen and the rest
    // kept old frames. Every pipeline renders at full scale, so 2D and 3D share one pipeline asset.
    public sealed class RenderScaleTests
    {
        [Test]
        public void Every_quality_level_renders_at_full_scale()
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
                Assert.That(new SerializedObject(pipeline).FindProperty("m_RenderScale").floatValue, Is.EqualTo(1f),
                    $"{name}: {pipeline.name} render scale");
            }
        }
    }
}
