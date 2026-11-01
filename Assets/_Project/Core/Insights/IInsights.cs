using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Insights
{
    // Crash reports and analytics for the game. Every call is a no-op while the SDK is off (editor, automation runs,
    // no Firebase config). Call from the main thread; RecordException and Log may be called from any thread.
    public interface IInsights
    {
        // True when the SDK started and the game has not turned collection off
        bool IsEnabled { get; }

        // Sent only while analytics is on (consent granted and collection enabled). Names: a letter, then up to 39
        // letters, digits or underscores. Never put player data in names or parameters.
        void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null);

        // An id the game owns (never an email or a name); null clears it
        void SetUserId(string id);

        // A non-fatal crash report with the exception's stack
        void RecordException(Exception exception);

        // A breadcrumb shown with the next crash report
        void Log(string message);

        // The game's own switch for both crash reports and analytics (on by default)
        void SetCollectionEnabled(bool enabled);
    }
}
