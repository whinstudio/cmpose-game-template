using System.IO;
using Kturker.UnityBase.Core.About;
using NUnit.Framework;
using UnityEditor;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class AppConfigTests
    {
        private const string AssetPath = "Assets/_Project/Core/About/AppConfig.asset";

        [Test]
        public void The_template_config_has_the_licences_and_no_links()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AssetPath);
            Assert.That(config, Is.Not.Null);

            Assert.That(config.ThirdPartyNotices.text, Does.Contain("VContainer").And.Contain("UniTask"));
            Assert.That(config.PrivacyUrl, Is.Empty);
            Assert.That(config.TermsUrl, Is.Empty);
            Assert.That(config.SupportUrl, Is.Empty);
        }

        // The generator edits these lines, so they must exist in the asset
        [Test]
        public void The_template_config_leaves_the_store_settings_and_links_blank()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AssetPath);

            Assert.That(config.MinVersionUrl, Is.Empty);
            Assert.That(config.AppStoreId, Is.Empty);
            var text = File.ReadAllText(AssetPath);
            foreach (var field in new[] { "privacyUrl", "termsUrl", "supportUrl", "minVersionUrl", "appStoreId" })
                Assert.That(text, Does.Match($@"(?m)^  {field}: ?$"), field);
        }
    }
}
