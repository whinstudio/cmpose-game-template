#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Insights
{
    // Firebase through Plugins/iOS/CmposeInsights.mm, on Unity's main thread. A bridge error (export missing, native
    // failure) is logged once and turns insights off for the run. Every line it logs starts with "Insights", so
    // Insights never captures it.
    public sealed class IosInsightsGateway : IInsightsGateway
    {
        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CmposeInsightsStart();

        [DllImport("__Internal")]
        private static extern void CmposeInsightsSetAnalytics([MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport("__Internal")]
        private static extern void CmposeInsightsSetCrashlytics([MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport("__Internal")]
        private static extern void CmposeInsightsLogEvent(string name, string paramsJson);

        [DllImport("__Internal")]
        private static extern void CmposeInsightsSetUserId(string id);

        [DllImport("__Internal")]
        private static extern void CmposeInsightsLog(string message);

        [DllImport("__Internal")]
        private static extern void CmposeInsightsRecord(string type, string message, string framesTsv);

        private readonly ILog log;
        private bool failed;

        public IosInsightsGateway(ILog log) => this.log = log;

        public bool Start()
        {
            try
            {
                if (CmposeInsightsStart()) return true;
            }
            catch (Exception e)
            {
                log.Warning($"Insights bridge did not start: {e.Message}");
            }
            failed = true;
            return false;
        }

        public void SetAnalyticsEnabled(bool enabled) => Call("setAnalyticsEnabled", () => CmposeInsightsSetAnalytics(enabled));

        public void SetCrashlyticsEnabled(bool enabled) => Call("setCrashlyticsEnabled", () => CmposeInsightsSetCrashlytics(enabled));

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) =>
            Call("logEvent", () => CmposeInsightsLogEvent(name, IosInsightsFormat.ToJson(parameters)));

        public void SetUserId(string id) => Call("setUserId", () => CmposeInsightsSetUserId(id));

        public void RecordException(string type, string message, IReadOnlyList<StackFrame> frames) =>
            Call("recordException", () => CmposeInsightsRecord(type, message, IosInsightsFormat.ToTsv(frames)));

        public void Log(string message) => Call("log", () => CmposeInsightsLog(message));

        private void Call(string method, Action action)
        {
            if (failed) return;
            try
            {
                action();
            }
            catch (Exception e)
            {
                failed = true;
                log.Warning($"Insights turned off after a bridge error ({method}): {e.Message}");
            }
        }
    }
}
#endif
