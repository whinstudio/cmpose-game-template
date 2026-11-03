using System.IO;
using System.Text.RegularExpressions;
using Kturker.UnityBase.Core.About;
using NUnit.Framework;
using UnityEditor;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class AppConfigTests
    {
        private const string AssetPath = "Assets/_Project/Core/About/AppConfig.asset";

        // Blank in the template; a generated game's branding step (Compass) writes its privacy page and Support link
        private const string LinkPattern = @"(https://\S+)?";

        [Test]
        public void The_template_config_has_the_licences_and_no_links_besides_the_branded_ones()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AssetPath);
            Assert.That(config, Is.Not.Null);

            Assert.That(config.ThirdPartyNotices.text, Does.Contain("VContainer").And.Contain("UniTask"));
            Assert.That(config.PrivacyUrl, Does.Match($"^{LinkPattern}$"));
            Assert.That(config.TermsUrl, Is.Empty);
            Assert.That(config.SupportUrl, Does.Match($"^{LinkPattern}$"));
        }

        // The generator and the branding step edit these lines, so they must exist in the asset
        [Test]
        public void The_template_config_leaves_the_store_settings_blank_and_the_links_blank_or_branded()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AssetPath);

            Assert.That(config.MinVersionUrl, Is.Empty);
            Assert.That(config.AppStoreId, Is.Empty);
            var text = File.ReadAllText(AssetPath);
            foreach (var field in new[] { "termsUrl", "minVersionUrl", "appStoreId" })
                Assert.That(text, Does.Match($@"(?m)^  {field}: ?$"), field);
            foreach (var field in new[] { "privacyUrl", "supportUrl" })
                Assert.That(Regex.IsMatch(text, $@"(?m)^  {field}: ?{LinkPattern}$"), Is.True, field);
        }
    }
}
