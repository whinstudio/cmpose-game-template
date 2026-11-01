using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Kturker.UnityBase.Core.Domain;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Insights
{
    // Pure logic over the platform gateway. Start starts the SDK once (never when disabled: editor, automation runs);
    // captured logs and recorded exceptions are queued from any thread and reach the gateway in Tick, on the main
    // thread. Analytics is on only while consent is granted and the game keeps collection on; crash reports follow
    // only the game's switch. At most SessionExceptionCap exceptions are reported per session.
    public sealed class Insights : IInsights, IStartable, ITickable, IDisposable
    {
        public const int SessionExceptionCap = 20;
        public const string OffWarning = "Insights off: add google-services.json / GoogleService-Info.plist (see README)";

        // UnityLog's prefix and this service's name: every line the service logs starts with it and is never captured
        private const string OwnLinePrefix = "[UnityBase] Insights";
        private const int MaxEventNameLength = 40;

        private static readonly IReadOnlyDictionary<string, object> NoParameters = new Dictionary<string, object>();

        private readonly IInsightsGateway gateway;
        private readonly IAnalyticsConsent consent;
        private readonly ILog log;
        private readonly bool startAllowed;
        private readonly bool releaseBuild;
        private readonly ILogSource logSource;
        private readonly ConcurrentQueue<Entry> queue = new ConcurrentQueue<Entry>();
        private bool started;
        private bool available;
        private bool subscribed;
        private bool collectionEnabled = true;
        private bool analyticsOn;
        private bool capWarned;
        private int exceptions;
        // Read by the threads that log, written on the main thread
        private volatile bool capturing;

        public Insights(IInsightsGateway gateway, IAnalyticsConsent consent, ILog log, bool enabled, bool releaseBuild,
            ILogSource logSource)
        {
            this.gateway = gateway;
            this.consent = consent;
            this.log = log;
            startAllowed = enabled;
            this.releaseBuild = releaseBuild;
            this.logSource = logSource;
        }

        public bool IsEnabled => available && collectionEnabled;

        public void Start()
        {
            if (started) return;
            started = true;
            available = startAllowed && gateway.Start();
            log.Marker($"CMPOSE_INSIGHTS available={(available ? "true" : "false")}");
            if (!available)
            {
                if (startAllowed && releaseBuild) log.Warning(OffWarning);
                return;
            }
            gateway.SetCrashlyticsEnabled(collectionEnabled);
            analyticsOn = AnalyticsAllowed;
            gateway.SetAnalyticsEnabled(analyticsOn);
            consent.Changed += OnConsentChanged;
            logSource.Received += OnLogReceived;
            subscribed = true;
            capturing = collectionEnabled;
        }

        public void Tick()
        {
            if (!available) return;
            while (queue.TryDequeue(out var entry))
            {
                if (!collectionEnabled) continue;
                if (entry.Exception) gateway.RecordException(entry.Type, entry.Message, entry.Frames);
                else gateway.Log(entry.Message);
            }
            if (!capWarned && Volatile.Read(ref exceptions) > SessionExceptionCap)
            {
                capWarned = true;
                log.Warning($"Insights: over {SessionExceptionCap} exceptions this session, the rest are not reported");
            }
        }

        public void Dispose()
        {
            capturing = false;
            if (subscribed)
            {
                subscribed = false;
                consent.Changed -= OnConsentChanged;
                logSource.Received -= OnLogReceived;
            }
            available = false;
            while (queue.TryDequeue(out _)) { }
        }

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!available) return;
            if (!ValidEventName(name))
            {
                log.Warning($"Insights: event name '{name}' is not valid, not sent");
                return;
            }
            if (analyticsOn) gateway.LogEvent(name, parameters ?? NoParameters);
        }

        public void SetUserId(string id)
        {
            if (available) gateway.SetUserId(id);
        }

        public void RecordException(Exception exception)
        {
            if (exception == null || !capturing) return;
            EnqueueException(exception.GetType().Name, exception.Message, exception.StackTrace);
        }

        public void Log(string message)
        {
            if (capturing && message != null) queue.Enqueue(Entry.Breadcrumb(message));
        }

        public void SetCollectionEnabled(bool enabled)
        {
            if (collectionEnabled == enabled) return;
            collectionEnabled = enabled;
            if (!available) return;
            capturing = enabled;
            gateway.SetCrashlyticsEnabled(enabled);
            ApplyAnalytics();
        }

        private bool AnalyticsAllowed => collectionEnabled && consent.Granted == true;

        private void OnConsentChanged()
        {
            if (available) ApplyAnalytics();
        }

        private void ApplyAnalytics()
        {
            var on = AnalyticsAllowed;
            if (on == analyticsOn) return;
            analyticsOn = on;
            gateway.SetAnalyticsEnabled(on);
        }

        // Any thread: only queues
        private void OnLogReceived(string message, string stack, LogKind kind)
        {
            if (!capturing || kind == LogKind.Info || message == null) return;
            if (message.StartsWith(OwnLinePrefix, StringComparison.Ordinal)) return;
            if (kind != LogKind.Exception)
            {
                queue.Enqueue(Entry.Breadcrumb(message));
                return;
            }
            // Unity logs an exception as "Type: message"
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0) EnqueueException(message.Substring(0, colon), message.Substring(colon + 2), stack);
            else EnqueueException("Exception", message, stack);
        }

        private void EnqueueException(string type, string message, string stack)
        {
            if (Interlocked.Increment(ref exceptions) > SessionExceptionCap) return;
            queue.Enqueue(Entry.ForException(type, message ?? string.Empty, UnityStackTrace.Parse(stack)));
        }

        private static bool ValidEventName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxEventNameLength || !IsAsciiLetter(name[0])) return false;
            foreach (var c in name)
                if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private readonly struct Entry
        {
            public readonly bool Exception;
            public readonly string Type;
            public readonly string Message;
            public readonly IReadOnlyList<StackFrame> Frames;

            private Entry(bool exception, string type, string message, IReadOnlyList<StackFrame> frames)
            {
                Exception = exception;
                Type = type;
                Message = message;
                Frames = frames;
            }

            public static Entry Breadcrumb(string message) => new Entry(false, null, message, null);

            public static Entry ForException(string type, string message, IReadOnlyList<StackFrame> frames) =>
                new Entry(true, type, message, frames);
        }
    }
}
