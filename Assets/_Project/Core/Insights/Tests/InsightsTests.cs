using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kturker.UnityBase.Tests.EditMode;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    public sealed class InsightsTests
    {
        private const string EditorTrace = "Game.Player.Jump () (at Assets/_Project/Player.cs:42)\nGame.Loop.Tick ()";

        private FakeInsightsGateway gateway;
        private FakeAnalyticsConsent consent;
        private FakeLogSource source;
        private FakeLog log;

        [SetUp]
        public void Create()
        {
            gateway = new FakeInsightsGateway();
            consent = new FakeAnalyticsConsent { Granted = true };
            source = new FakeLogSource();
            log = new FakeLog();
        }

        private Insights Make(bool enabled = true, bool release = false) =>
            new Insights(gateway, consent, log, enabled, release, source);

        // A started service whose start-up calls are already cleared
        private Insights Started()
        {
            var insights = Make();
            insights.Start();
            gateway.Calls.Clear();
            return insights;
        }

        private IEnumerable<string> Markers => log.Lines.Where(l => l.StartsWith("MARKER CMPOSE_"));
        private IEnumerable<string> Warnings => log.Lines.Where(l => l.StartsWith("WARNING "));

        [Test]
        public void Disabled_never_starts_the_gateway_and_marks_unavailable()
        {
            var insights = Make(enabled: false, release: true);
            insights.Start();
            source.Raise("Boom: x", EditorTrace, LogKind.Exception);
            insights.Tick();

            CollectionAssert.IsEmpty(gateway.Calls);
            CollectionAssert.AreEqual(new[] { "MARKER CMPOSE_INSIGHTS available=false" }, Markers);
            Assert.IsFalse(insights.IsEnabled);
            CollectionAssert.IsEmpty(Warnings);
        }

        [Test]
        public void Unconfigured_release_warns_once()
        {
            gateway.Configured = false;
            var insights = Make(release: true);
            insights.Start();

            CollectionAssert.AreEqual(new[] { "MARKER CMPOSE_INSIGHTS available=false" }, Markers);
            CollectionAssert.AreEqual(new[] { "WARNING " + Insights.OffWarning }, Warnings);
            Assert.IsFalse(insights.IsEnabled);

            log.Lines.Clear();
            Make(release: false).Start();
            CollectionAssert.AreEqual(new[] { "MARKER CMPOSE_INSIGHTS available=false" }, Markers);
            CollectionAssert.IsEmpty(Warnings);
        }

        [Test]
        public void Configured_marks_available_and_turns_crashlytics_on()
        {
            var insights = Make();
            insights.Start();

            CollectionAssert.AreEqual(new[] { "MARKER CMPOSE_INSIGHTS available=true" }, Markers);
            Assert.AreEqual("start", gateway.Calls[0]);
            CollectionAssert.Contains(gateway.Calls, "crashlytics True");
            Assert.IsTrue(insights.IsEnabled);
        }

        [Test]
        public void Analytics_waits_for_consent_and_follows_changes()
        {
            consent.Granted = null;
            var insights = Make();
            insights.Start();
            CollectionAssert.DoesNotContain(gateway.Calls, "analytics True");

            gateway.Calls.Clear();
            consent.Answer(true);
            CollectionAssert.AreEqual(new[] { "analytics True" }, gateway.Calls);

            gateway.Calls.Clear();
            consent.Answer(false);
            CollectionAssert.AreEqual(new[] { "analytics False" }, gateway.Calls);

            gateway.Calls.Clear();
            consent.Answer(null);
            CollectionAssert.IsEmpty(gateway.Calls);
        }

        [Test]
        public void Granted_consent_turns_analytics_on_at_start()
        {
            Make().Start();

            CollectionAssert.Contains(gateway.Calls, "analytics True");
        }

        [Test]
        public void Collection_disabled_by_the_game_overrides_consent()
        {
            var insights = Started();
            insights.SetCollectionEnabled(false);

            CollectionAssert.AreEquivalent(new[] { "crashlytics False", "analytics False" }, gateway.Calls);
            Assert.IsFalse(insights.IsEnabled);

            gateway.Calls.Clear();
            consent.Answer(true);
            insights.LogEvent("level_end");
            insights.RecordException(new InvalidOperationException("x"));
            source.Raise("Boom: x", EditorTrace, LogKind.Exception);
            insights.Tick();
            CollectionAssert.IsEmpty(gateway.Calls);

            insights.SetCollectionEnabled(true);
            CollectionAssert.AreEquivalent(new[] { "crashlytics True", "analytics True" }, gateway.Calls);
        }

        [Test]
        public void Collection_disabled_before_start_is_applied_at_start()
        {
            var insights = Make();
            insights.SetCollectionEnabled(false);
            insights.Start();

            CollectionAssert.Contains(gateway.Calls, "crashlytics False");
            CollectionAssert.DoesNotContain(gateway.Calls, "crashlytics True");
            CollectionAssert.DoesNotContain(gateway.Calls, "analytics True");
        }

        [Test]
        public void Exceptions_become_non_fatals_with_parsed_frames()
        {
            var insights = Started();
            source.Raise("NullReferenceException: x", EditorTrace, LogKind.Exception);
            CollectionAssert.IsEmpty(gateway.Calls);

            insights.Tick();

            CollectionAssert.AreEqual(new[] { "exception NullReferenceException x" }, gateway.Calls);
            var frame = gateway.Exceptions[0][0];
            Assert.AreEqual("Game.Player.Jump ()", frame.Symbol);
            Assert.AreEqual("Assets/_Project/Player.cs", frame.File);
            Assert.AreEqual(42, frame.Line);
        }

        [Test]
        public void An_exception_message_without_a_colon_is_typed_exception()
        {
            var insights = Started();
            source.Raise("something broke", EditorTrace, LogKind.Exception);
            insights.Tick();

            CollectionAssert.AreEqual(new[] { "exception Exception something broke" }, gateway.Calls);
        }

        [Test]
        public void Recorded_exceptions_reach_the_gateway_on_tick()
        {
            var insights = Started();
            Exception thrown;
            try
            {
                throw new InvalidOperationException("bad state");
            }
            catch (Exception e)
            {
                thrown = e;
            }
            insights.RecordException(thrown);
            insights.RecordException(null);
            CollectionAssert.IsEmpty(gateway.Calls);

            insights.Tick();

            CollectionAssert.AreEqual(new[] { "exception InvalidOperationException bad state" }, gateway.Calls);
            StringAssert.Contains(nameof(Recorded_exceptions_reach_the_gateway_on_tick), gateway.Exceptions[0][0].Symbol);
        }

        [Test]
        public void Warnings_and_errors_become_breadcrumbs_info_does_not()
        {
            var insights = Started();
            source.Raise("careful", "", LogKind.Warning);
            source.Raise("broken", "", LogKind.Error);
            source.Raise("hello", "", LogKind.Info);
            insights.Log("game note");
            insights.Tick();

            CollectionAssert.AreEqual(new[] { "log careful", "log broken", "log game note" }, gateway.Calls);
        }

        [Test]
        public void Background_thread_logs_reach_the_gateway_on_tick()
        {
            var insights = Started();
            Task.Run(() => source.Raise("Boom: off main", EditorTrace, LogKind.Exception)).Wait();

            CollectionAssert.IsEmpty(gateway.Calls);

            insights.Tick();

            CollectionAssert.AreEqual(new[] { "exception Boom off main" }, gateway.Calls);
        }

        [Test]
        public void Exceptions_over_the_session_cap_are_dropped_and_logged_once()
        {
            var insights = Started();
            for (var i = 0; i < Insights.SessionExceptionCap + 5; i++)
                source.Raise($"Boom: {i}", EditorTrace, LogKind.Exception);
            insights.Tick();
            source.Raise("Boom: later", EditorTrace, LogKind.Exception);
            insights.RecordException(new InvalidOperationException("later"));
            insights.Tick();

            Assert.AreEqual(Insights.SessionExceptionCap, gateway.Calls.Count(c => c.StartsWith("exception ")));
            var warnings = Warnings.ToList();
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(Insights.SessionExceptionCap.ToString(), warnings[0]);
        }

        [Test]
        public void Own_log_lines_are_not_captured()
        {
            var insights = Started();
            source.Raise("[UnityBase] Insights: something failed", "", LogKind.Error);
            source.Raise("[UnityBase] " + Insights.OffWarning, "", LogKind.Warning);
            insights.Tick();

            CollectionAssert.IsEmpty(gateway.Calls);
        }

        [Test]
        public void Nothing_reaches_the_gateway_when_unconfigured()
        {
            gateway.Configured = false;
            var insights = Make();
            insights.Start();
            gateway.Calls.Clear();

            insights.LogEvent("level_end");
            insights.SetUserId("player-1");
            insights.RecordException(new InvalidOperationException("x"));
            insights.Log("note");
            insights.SetCollectionEnabled(false);
            source.Raise("Boom: x", EditorTrace, LogKind.Exception);
            source.Raise("careful", "", LogKind.Warning);
            consent.Answer(true);
            insights.Tick();

            CollectionAssert.IsEmpty(gateway.Calls);
            Assert.IsFalse(insights.IsEnabled);
        }

        [Test]
        public void Events_reach_the_gateway_only_while_analytics_is_on()
        {
            consent.Granted = null;
            var insights = Started();
            insights.LogEvent("level_end");
            CollectionAssert.IsEmpty(gateway.Calls);

            consent.Answer(true);
            gateway.Calls.Clear();
            insights.LogEvent("level_end", new Dictionary<string, object> { ["level"] = 3 });
            insights.LogEvent("level_start");
            CollectionAssert.AreEqual(new[] { "event level_end 1", "event level_start 0" }, gateway.Calls);
        }

        [Test]
        public void Invalid_event_names_are_dropped_with_a_warning()
        {
            var insights = Started();
            insights.LogEvent(null);
            insights.LogEvent("");
            insights.LogEvent("1st_level");
            insights.LogEvent("level end");
            insights.LogEvent(new string('a', 41));

            CollectionAssert.IsEmpty(gateway.Calls);
            Assert.AreEqual(5, Warnings.Count());
        }

        [Test]
        public void Dispose_unsubscribes()
        {
            consent.Granted = null;
            var insights = Started();
            source.Raise("Boom: queued", EditorTrace, LogKind.Exception);
            insights.Dispose();

            source.Raise("Boom: x", EditorTrace, LogKind.Exception);
            source.Raise("careful", "", LogKind.Warning);
            consent.Answer(true);
            insights.SetCollectionEnabled(false);
            insights.LogEvent("level_end");
            insights.Tick();

            CollectionAssert.IsEmpty(gateway.Calls);
            Assert.IsFalse(insights.IsEnabled);
        }
    }
}
