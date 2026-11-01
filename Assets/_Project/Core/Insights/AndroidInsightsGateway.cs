#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights
{
    // Firebase through Plugins/Android/CmposeInsights.androidlib (dev.cmpose.insights.CmposeInsights), on Unity's main
    // thread. A bridge error (class missing, JNI failure, Firebase throwing) is logged once and turns insights off for
    // the run. Every line it logs starts with "Insights", so Insights never captures it.
    public sealed class AndroidInsightsGateway : IInsightsGateway
    {
        private const string Bridge = "dev.cmpose.insights.CmposeInsights";

        private readonly ILog log;
        private AndroidJavaClass bridge;
        private bool failed;

        public AndroidInsightsGateway(ILog log) => this.log = log;

        public bool Start()
        {
            try
            {
                bridge = new AndroidJavaClass(Bridge);
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                if (bridge.CallStatic<bool>("start", activity)) return true;
            }
            catch (Exception e)
            {
                log.Warning($"Insights bridge did not start: {e.Message}");
            }
            failed = true;
            bridge?.Dispose();
            return false;
        }

        public void SetAnalyticsEnabled(bool enabled) => Call("setAnalyticsEnabled", enabled);

        public void SetCrashlyticsEnabled(bool enabled) => Call("setCrashlyticsEnabled", enabled);

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters)
        {
            if (failed) return;
            try
            {
                using var bundle = ToBundle(parameters);
                bridge.CallStatic("logEvent", name, bundle);
            }
            catch (Exception e)
            {
                Fail("logEvent", e);
            }
        }

        public void SetUserId(string id) => Call("setUserId", id);

        public void RecordException(string type, string message, IReadOnlyList<StackFrame> frames)
        {
            var symbols = new string[frames.Count];
            var files = new string[frames.Count];
            var lines = new int[frames.Count];
            for (var i = 0; i < frames.Count; i++)
            {
                symbols[i] = frames[i].Symbol ?? string.Empty;
                files[i] = frames[i].File;
                lines[i] = frames[i].Line;
            }
            Call("recordException", type, message, symbols, files, lines);
        }

        public void Log(string message) => Call("log", message);

        private void Call(string method, params object[] args)
        {
            if (failed) return;
            try
            {
                bridge.CallStatic(method, args);
            }
            catch (Exception e)
            {
                Fail(method, e);
            }
        }

        private void Fail(string method, Exception e)
        {
            failed = true;
            log.Warning($"Insights turned off after a bridge error ({method}): {e.Message}");
        }

        // Firebase takes strings, longs and doubles; any other value is sent as its text
        private static AndroidJavaObject ToBundle(IReadOnlyDictionary<string, object> parameters)
        {
            var bundle = new AndroidJavaObject("android.os.Bundle");
            foreach (var pair in parameters)
            {
                switch (pair.Value)
                {
                    case null:
                        break;
                    case string text:
                        bundle.Call("putString", pair.Key, text);
                        break;
                    case long or int or short or byte or sbyte or ushort or uint:
                        bundle.Call("putLong", pair.Key, Convert.ToInt64(pair.Value));
                        break;
                    case double or float or decimal:
                        bundle.Call("putDouble", pair.Key, Convert.ToDouble(pair.Value));
                        break;
                    default:
                        bundle.Call("putString", pair.Key, Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                }
            }
            return bundle;
        }
    }
}
#endif
