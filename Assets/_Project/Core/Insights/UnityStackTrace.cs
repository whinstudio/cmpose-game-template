using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kturker.UnityBase.Core.Insights
{
    // Turns a stack into frames. Reads Unity's log format ("Symbol (at File:Line)") and .NET's ("at Symbol [0x..] in
    // File:Line"); a position Unity cannot know ("<hash>:0" in IL2CPP release builds) gives no file. Never throws.
    public static class UnityStackTrace
    {
        private static readonly IReadOnlyList<StackFrame> None = new StackFrame[0];

        public static IReadOnlyList<StackFrame> Parse(string unityStackTrace)
        {
            if (string.IsNullOrEmpty(unityStackTrace)) return None;
            var frames = new List<StackFrame>();
            foreach (var raw in unityStackTrace.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("---", StringComparison.Ordinal)) continue;
                frames.Add(ParseLine(line));
            }
            return frames;
        }

        private static StackFrame ParseLine(string line)
        {
            var at = line.LastIndexOf(" (at ", StringComparison.Ordinal);
            if (at > 0 && line.EndsWith(")", StringComparison.Ordinal))
                return Frame(line.Substring(0, at), line.Substring(at + 5, line.Length - at - 6));

            if (line.StartsWith("at ", StringComparison.Ordinal)) line = line.Substring(3).TrimStart();
            var symbol = line;
            string position = null;
            var inAt = line.LastIndexOf(" in ", StringComparison.Ordinal);
            if (inAt > 0)
            {
                symbol = line.Substring(0, inAt);
                position = line.Substring(inAt + 4);
            }
            // Mono's IL offset: "Jump () [0x00012]"
            var offset = symbol.LastIndexOf(" [0x", StringComparison.Ordinal);
            if (offset > 0 && symbol.EndsWith("]", StringComparison.Ordinal)) symbol = symbol.Substring(0, offset);
            return Frame(symbol, position);
        }

        // "File:Line"; no file when it is empty, a "<hash>" or the line is not a number
        private static StackFrame Frame(string symbol, string position)
        {
            symbol = symbol.Trim();
            if (string.IsNullOrEmpty(position)) return new StackFrame(symbol, null, 0);
            var colon = position.LastIndexOf(':');
            if (colon <= 0) return new StackFrame(symbol, null, 0);
            var file = position.Substring(0, colon).Trim();
            if (file.Length == 0 || file[0] == '<') return new StackFrame(symbol, null, 0);
            if (!int.TryParse(position.Substring(colon + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return new StackFrame(symbol, null, 0);
            return new StackFrame(symbol, file, number);
        }
    }
}
