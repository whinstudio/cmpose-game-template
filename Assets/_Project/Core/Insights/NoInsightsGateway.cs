using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Insights
{
    // The editor and automation runs: there is no SDK, so it never starts (Insights never calls the rest then)
    public sealed class NoInsightsGateway : IInsightsGateway
    {
        public bool Start() => false;

        public void SetAnalyticsEnabled(bool enabled) { }

        public void SetCrashlyticsEnabled(bool enabled) { }

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) { }

        public void SetUserId(string id) { }

        public void RecordException(string type, string message, IReadOnlyList<StackFrame> frames) { }

        public void Log(string message) { }
    }
}
