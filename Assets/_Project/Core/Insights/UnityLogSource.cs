using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights
{
    // ILogSource over Application.logMessageReceivedThreaded: raised on whichever thread logged, Insights queues it.
    // Asserts count as errors (they are not exceptions with a "Type: message" line); plain logs as Info.
    public sealed class UnityLogSource : ILogSource, IDisposable
    {
        private bool subscribed;

        public UnityLogSource()
        {
            Application.logMessageReceivedThreaded += OnLog;
            subscribed = true;
        }

        public event Action<string, string, LogKind> Received;

        public void Dispose()
        {
            if (!subscribed) return;
            subscribed = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        public static LogKind KindOf(LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    return LogKind.Exception;
                case LogType.Error:
                case LogType.Assert:
                    return LogKind.Error;
                case LogType.Warning:
                    return LogKind.Warning;
                default:
                    return LogKind.Info;
            }
        }

        private void OnLog(string message, string stack, LogType type) => Received?.Invoke(message, stack, KindOf(type));
    }
}
