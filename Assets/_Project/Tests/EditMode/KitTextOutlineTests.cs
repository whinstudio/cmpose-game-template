using System.Collections.Generic;
using System.Linq;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class KitTextOutlineTests
    {
        private const string Inter = "Assets/_Project/Core/Presentation/Fonts/Inter/Inter-Regular SDF.asset";

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            foreach (var o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            KitTextOutline.DestroyMaterialsForTests();
        }

        private static TMP_FontAsset Font()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Inter);
            Assert.That(font, Is.Not.Null, Inter);
            return font;
        }

        private TMP_Text Text(TMP_FontAsset font)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            made.Add(go);
            var text = go.GetComponent<TMP_Text>();
            text.font = font;
            return text;
        }

        [Test]
        public void Same_outline_shares_one_material()
        {
            var font = Font();
            var first = Text(font);
            var second = Text(font);
            var colour = new Color(0.3f, 0.1f, 0.2f, 1f);
            KitTextOutline.Apply(first, colour, 0.12f, false);
            KitTextOutline.Apply(second, colour, 0.12f, false);

            Assert.That(KitTextOutline.Has(first), Is.True, "outlined");
            Assert.That(first.fontSharedMaterial, Is.Not.SameAs(font.material), "a copy, not the font's own");
            Assert.That(second.fontSharedMaterial, Is.SameAs(first.fontSharedMaterial), "one material for one outline");
            var material = first.fontSharedMaterial;
            Assert.That(material.IsKeywordEnabled("OUTLINE_ON"), Is.True, "outline keyword");
            Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"), Is.False, "no underlay");
            Assert.That(material.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(0.12f).Within(1e-5f), "width");
            // A material keeps its colours in the project's colour space: back within a rounding
            var drawn = material.GetColor(ShaderUtilities.ID_OutlineColor);
            for (var i = 0; i < 4; i++)
                Assert.That(drawn[i], Is.EqualTo(colour[i]).Within(1e-4f), $"colour [{i}]");
            Assert.That(material.GetTexture(ShaderUtilities.ID_MainTex), Is.SameAs(font.material.GetTexture(ShaderUtilities.ID_MainTex)), "the font's atlas");

            KitTextOutline.Apply(second, Color.black, 0.12f, false);
            Assert.That(second.fontSharedMaterial, Is.Not.SameAs(first.fontSharedMaterial), "another colour, another material");
            KitTextOutline.Apply(second, colour, 0.12f, true);
            Assert.That(second.fontSharedMaterial, Is.Not.SameAs(first.fontSharedMaterial), "an underlay, another material");
            Assert.That(second.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), Is.True, "underlay keyword");
        }

        [Test]
        public void Clear_restores_the_fonts_material()
        {
            var font = Font();
            var text = Text(font);
            KitTextOutline.Apply(text, Color.black, 0.12f, true);
            KitTextOutline.Clear(text);

            Assert.That(text.fontSharedMaterial, Is.SameAs(font.material));
            Assert.That(KitTextOutline.Has(text), Is.False);

            // A material the outline did not set is left alone
            var own = new Material(font.material);
            made.Add(own);
            text.fontSharedMaterial = own;
            KitTextOutline.Clear(text);
            Assert.That(text.fontSharedMaterial, Is.SameAs(own), "not the outline's");
            Assert.That(KitTextOutline.Has(text), Is.False, "not the outline's");
        }

        [Test]
        public void The_fonts_own_material_is_untouched()
        {
            var font = Font();
            var text = Text(font);
            KitTextOutline.Apply(text, Color.red, 0.2f, true);

            var own = font.material;
            Assert.That(own.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(0f), "width");
            Assert.That(own.IsKeywordEnabled("OUTLINE_ON"), Is.False, "outline keyword");
            Assert.That(own.IsKeywordEnabled("UNDERLAY_ON"), Is.False, "underlay keyword");
            Assert.That(text.fontSharedMaterial, Is.Not.SameAs(own), "the text draws with a copy");
        }

        // A player build compiles a shader's keyword variants only for the materials it ships: the bundled fonts' SDF
        // shader must sit on a material under a Resources folder with OUTLINE_ON alone (a button label's outline) and with
        // OUTLINE_ON and UNDERLAY_ON (a title on a card's band), or the outlines vanish on a device
        [Test]
        public void Outline_and_underlay_variants_ship_in_resources()
        {
            var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/_Project" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(font => font != null && font.material != null).ToList();
            Assert.That(fonts, Is.Not.Empty, "bundled fonts");
            var shaders = fonts.Select(font => font.material.shader).Distinct().ToList();
            Assert.That(shaders.Select(shader => shader.name), Is.EqualTo(new[] { "TextMeshPro/Mobile/Distance Field" }),
                "the bundled fonts draw with TMP's mobile SDF shader");
            var shipped = AssetDatabase.FindAssets("t:Material").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/Resources/")).Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(material => material != null).ToList();
            foreach (var shader in shaders)
            {
                var own = shipped.Where(material => material.shader == shader).ToList();
                Assert.That(own.Any(m => m.IsKeywordEnabled("OUTLINE_ON") && !m.IsKeywordEnabled("UNDERLAY_ON")), Is.True,
                    $"{shader.name}: a Resources material with OUTLINE_ON alone (TextMesh Pro/Resources: the Outline material)");
                Assert.That(own.Any(m => m.IsKeywordEnabled("OUTLINE_ON") && m.IsKeywordEnabled("UNDERLAY_ON")), Is.True,
                    $"{shader.name}: a Resources material with OUTLINE_ON and UNDERLAY_ON (TextMesh Pro/Resources: the Drop Shadow material)");
            }
        }

        // A fixture's teardown leaves no outline wish behind: the text no longer follows its font changes
        [Test]
        public void Destroying_the_materials_forgets_the_outlined_texts()
        {
            var text = Text(Font());
            var themed = text.gameObject.AddComponent<ThemedText>();
            KitTextOutline.Apply(text, Color.black, 0.12f, false);
            KitTextOutline.DestroyMaterialsForTests();
            text.fontSharedMaterial = text.font.material;

            themed.SetBold();
            Assert.That(KitTextOutline.Has(text), Is.False, "a font change after the teardown puts no outline back");
        }
    }
}
