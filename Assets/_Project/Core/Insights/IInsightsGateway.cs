using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Insights
{
    // The only place that touches the native SDK. Called on Unity's main thread only; nothing throws (a failure is a
    // warning in the log). Analytics collection is off until SetAnalyticsEnabled(true).
    public interface IInsightsGateway
    {
        // Starts Firebase when its config is in the build: true when it started
        bool Start();

        void SetAnalyticsEnabled(bool enabled);

        void SetCrashlyticsEnabled(bool enabled);

        void LogEvent(string name, IReadOnlyDictionary<string, object> parameters);

        void SetUserId(string id);

        void RecordException(string type, string message, IReadOnlyList<StackFrame> frames);

        void Log(string message);
    }
}
