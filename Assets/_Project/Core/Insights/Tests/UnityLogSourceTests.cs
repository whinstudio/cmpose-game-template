using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    public sealed class UnityLogSourceTests
    {
        [TestCase(LogType.Exception, LogKind.Exception)]
        [TestCase(LogType.Error, LogKind.Error)]
        [TestCase(LogType.Assert, LogKind.Error)]
        [TestCase(LogType.Warning, LogKind.Warning)]
        [TestCase(LogType.Log, LogKind.Info)]
        public void Maps_each_unity_log_type(LogType type, LogKind kind)
        {
            Assert.That(UnityLogSource.KindOf(type), Is.EqualTo(kind));
        }

        [Test]
        public void Forwards_unity_logs_until_disposed()
        {
            var kinds = new List<LogKind>();
            var source = new UnityLogSource();
            source.Received += (message, _, kind) =>
            {
                if (message.StartsWith("insights log source test", StringComparison.Ordinal)) kinds.Add(kind);
            };
            Debug.LogWarning("insights log source test 1");
            source.Dispose();
            source.Dispose();
            Debug.LogWarning("insights log source test 2");
            Assert.That(kinds, Is.EqualTo(new[] { LogKind.Warning }));
        }
    }
}
