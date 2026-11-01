using System;
using System.Linq;
using System.Xml;
using Kturker.UnityBase.Core.Insights.Editor;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    public sealed class GoogleServicesResourcesTests
    {
        private const string Package = "studio.whin.game";
        private const string BuildId = "0123456789abcdef0123456789abcdef";

        // Two Android apps in one project, as the Firebase console writes the file
        private const string Json = @"{
  ""project_info"": {
    ""project_number"": ""123456789012"",
    ""project_id"": ""cmpose-test"",
    ""storage_bucket"": ""cmpose-test.firebasestorage.app""
  },
  ""client"": [
    {
      ""client_info"": {
        ""mobilesdk_app_id"": ""1:123456789012:android:other"",
        ""android_client_info"": { ""package_name"": ""studio.whin.other"" }
      },
      ""oauth_client"": [],
      ""api_key"": [ { ""current_key"": ""other-key"" } ]
    },
    {
      ""client_info"": {
        ""mobilesdk_app_id"": ""1:123456789012:android:game"",
        ""android_client_info"": { ""package_name"": ""studio.whin.game"" }
      },
      ""oauth_client"": [],
      ""api_key"": [ { ""current_key"": ""game-key"" } ],
      ""services"": { ""appinvite_service"": { ""other_platform_oauth_client"": [] } }
    }
  ],
  ""configuration_version"": ""1""
}";

        private static XmlElement[] Strings(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.GetElementsByTagName("string").OfType<XmlElement>().ToArray();
        }

        private static string Value(string xml, string name) =>
            Strings(xml).FirstOrDefault(e => e.GetAttribute("name") == name)?.InnerText;

        [Test]
        public void Writes_the_values_the_google_services_plugin_writes()
        {
            var xml = GoogleServicesResources.ToValuesXml(Json, Package, BuildId);

            Assert.AreEqual("1:123456789012:android:game", Value(xml, "google_app_id"));
            Assert.AreEqual("123456789012", Value(xml, "gcm_defaultSenderId"));
            Assert.AreEqual("game-key", Value(xml, "google_api_key"));
            Assert.AreEqual("game-key", Value(xml, "google_crash_reporting_api_key"));
            Assert.AreEqual("cmpose-test", Value(xml, "project_id"));
            Assert.AreEqual("cmpose-test.firebasestorage.app", Value(xml, "google_storage_bucket"));
            Assert.AreEqual(BuildId, Value(xml, "com.crashlytics.android.build_id"));
            Assert.AreEqual(BuildId, Value(xml, "com.google.firebase.crashlytics.mapping_file_id"));
            Assert.IsTrue(Strings(xml).All(e => e.GetAttribute("translatable") == "false"));
            StringAssert.DoesNotContain("other", xml);
        }

        [Test]
        public void No_client_for_the_package_fails_with_the_package_in_the_message()
        {
            var e = Assert.Throws<InvalidOperationException>(() => GoogleServicesResources.ToValuesXml(Json, "studio.whin.missing", BuildId));

            StringAssert.Contains("studio.whin.missing", e.Message);
        }

        [Test]
        public void Missing_optional_storage_bucket_is_left_out()
        {
            var json = Json.Replace(@"""storage_bucket"": ""cmpose-test.firebasestorage.app""", @"""project_name"": ""x""");

            var xml = GoogleServicesResources.ToValuesXml(json, Package, BuildId);

            Assert.IsNull(Value(xml, "google_storage_bucket"));
            Assert.AreEqual("cmpose-test", Value(xml, "project_id"));
        }

        [Test]
        public void Invalid_json_fails_with_a_clear_message()
        {
            var e = Assert.Throws<InvalidOperationException>(() => GoogleServicesResources.ToValuesXml("{ not json", Package, BuildId));

            StringAssert.Contains("google-services.json", e.Message);
        }

        [Test]
        public void Values_are_escaped_for_android_resources()
        {
            var json = Json.Replace("game-key", @"a'b<c&\\d");

            var xml = GoogleServicesResources.ToValuesXml(json, Package, BuildId);

            Assert.AreEqual(@"a\'b<c&\\d", Value(xml, "google_api_key"));
        }
    }
}
