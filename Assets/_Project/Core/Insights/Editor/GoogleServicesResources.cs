using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights.Editor
{
    // google-services.json to the string resources the google-services Gradle plugin would write for one package,
    // plus the build id Crashlytics needs when its Gradle plugin is not applied. Pure: no file or project access.
    public static class GoogleServicesResources
    {
        // Crashlytics reads the first and falls back to the second (the older name)
        public const string MappingFileIdName = "com.google.firebase.crashlytics.mapping_file_id";
        public const string BuildIdName = "com.crashlytics.android.build_id";

        public static string ToValuesXml(string json, string packageName, string buildId)
        {
            GoogleServicesJson config;
            try
            {
                config = JsonUtility.FromJson<GoogleServicesJson>(json ?? string.Empty);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"google-services.json is not valid JSON: {e.Message}");
            }
            if (config?.project_info == null || config.client == null)
                throw new InvalidOperationException("google-services.json has no project_info or client: download it again from the Firebase console");

            var client = Find(config.client, packageName);
            if (client == null)
                throw new InvalidOperationException(
                    $"google-services.json has no Android app for the package {packageName}: register it in the Firebase console and download the file again");

            var values = new List<KeyValuePair<string, string>>
            {
                Pair("google_app_id", client.client_info.mobilesdk_app_id),
                Pair("gcm_defaultSenderId", config.project_info.project_number)
            };
            var apiKey = client.api_key != null && client.api_key.Length > 0 ? client.api_key[0].current_key : null;
            if (!string.IsNullOrEmpty(apiKey))
            {
                values.Add(Pair("google_api_key", apiKey));
                values.Add(Pair("google_crash_reporting_api_key", apiKey));
            }
            values.Add(Pair("project_id", config.project_info.project_id));
            if (!string.IsNullOrEmpty(config.project_info.storage_bucket))
                values.Add(Pair("google_storage_bucket", config.project_info.storage_bucket));
            values.Add(Pair(MappingFileIdName, buildId));
            values.Add(Pair(BuildIdName, buildId));

            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            xml.Append("<!-- Written by InsightsAndroidPostprocessor from Assets/Firebase/google-services.json -->\n");
            xml.Append("<resources>\n");
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value.Value)) continue;
                xml.Append("    <string name=\"").Append(value.Key).Append("\" translatable=\"false\">")
                    .Append(Escape(value.Value)).Append("</string>\n");
            }
            xml.Append("</resources>\n");
            return xml.ToString();
        }

        private static Client Find(Client[] clients, string packageName)
        {
            foreach (var client in clients)
                if (client?.client_info?.android_client_info?.package_name == packageName && !string.IsNullOrEmpty(client.client_info.mobilesdk_app_id))
                    return client;
            return null;
        }

        private static KeyValuePair<string, string> Pair(string name, string value) => new KeyValuePair<string, string>(name, value);

        // XML text plus Android's own escapes (quotes, backslash, a leading @ or ?)
        private static string Escape(string value)
        {
            var text = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '&': text.Append("&amp;"); break;
                    case '<': text.Append("&lt;"); break;
                    case '>': text.Append("&gt;"); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\'': text.Append("\\'"); break;
                    case '"': text.Append("\\\""); break;
                    default: text.Append(c); break;
                }
            }
            if (text.Length > 0 && (text[0] == '@' || text[0] == '?')) text.Insert(0, '\\');
            return text.ToString();
        }

        // The parts of google-services.json the resources come from; names as in the file
#pragma warning disable 0649
        [Serializable]
        private sealed class GoogleServicesJson
        {
            public ProjectInfo project_info;
            public Client[] client;
        }

        [Serializable]
        private sealed class ProjectInfo
        {
            public string project_number;
            public string project_id;
            public string storage_bucket;
        }

        [Serializable]
        private sealed class Client
        {
            public ClientInfo client_info;
            public ApiKey[] api_key;
        }

        [Serializable]
        private sealed class ClientInfo
        {
            public string mobilesdk_app_id;
            public AndroidClientInfo android_client_info;
        }

        [Serializable]
        private sealed class AndroidClientInfo
        {
            public string package_name;
        }

        [Serializable]
        private sealed class ApiKey
        {
            public string current_key;
        }
#pragma warning restore 0649
    }
}
