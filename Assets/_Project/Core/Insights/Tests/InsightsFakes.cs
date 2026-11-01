using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    // Calls records what reached the platform, in order; Exceptions keeps the frames of each RecordException
    public sealed class FakeInsightsGateway : IInsightsGateway
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<IReadOnlyList<StackFrame>> Exceptions = new List<IReadOnlyList<StackFrame>>();
        public bool Configured = true;

        public bool Start()
        {
            Calls.Add("start");
            return Configured;
        }

        public void SetAnalyticsEnabled(bool enabled) => Calls.Add($"analytics {enabled}");

        public void SetCrashlyticsEnabled(bool enabled) => Calls.Add($"crashlytics {enabled}");

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) =>
            Calls.Add($"event {name} {parameters?.Count ?? -1}");

        public void SetUserId(string id) => Calls.Add($"user {id}");

        public void RecordException(string type, string message, IReadOnlyList<StackFrame> frames)
        {
            Calls.Add($"exception {type} {message}");
            Exceptions.Add(frames);
        }

        public void Log(string message) => Calls.Add($"log {message}");
    }

    public sealed class FakeAnalyticsConsent : IAnalyticsConsent
    {
        public bool? Granted { get; set; }

        public event Action Changed;

        public void Answer(bool? granted)
        {
            Granted = granted;
            Changed?.Invoke();
        }
    }

    public sealed class FakeLogSource : ILogSource
    {
        public event Action<string, string, LogKind> Received;

        public void Raise(string message, string stack, LogKind kind) => Received?.Invoke(message, stack, kind);
    }
}
