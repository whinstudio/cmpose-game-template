using System;

namespace Kturker.UnityBase.Core.Insights
{
    public enum LogKind
    {
        Info,
        Warning,
        Error,
        Exception
    }

    // Every line the game logs (message, stack, kind), from any thread. The Unity adapter wraps
    // Application.logMessageReceivedThreaded.
    public interface ILogSource
    {
        event Action<string, string, LogKind> Received;
    }
}
