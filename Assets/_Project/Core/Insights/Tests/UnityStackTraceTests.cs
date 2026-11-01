using NUnit.Framework;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    public sealed class UnityStackTraceTests
    {
        [Test]
        public void Parses_editor_frames_with_file_and_line()
        {
            var frames = UnityStackTrace.Parse(
                "Game.Player.Jump () (at Assets/_Project/Player.cs:42)\nUnityEngine.Debug:LogException(Exception)");

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual("Game.Player.Jump ()", frames[0].Symbol);
            Assert.AreEqual("Assets/_Project/Player.cs", frames[0].File);
            Assert.AreEqual(42, frames[0].Line);
            Assert.AreEqual("UnityEngine.Debug:LogException(Exception)", frames[1].Symbol);
            Assert.IsNull(frames[1].File);
            Assert.AreEqual(0, frames[1].Line);
        }

        [Test]
        public void Parses_il2cpp_release_frames_without_file()
        {
            var frames = UnityStackTrace.Parse(
                "Game.Player.Jump () (at <00000000000000000000000000000000>:0)\nGame.Loop.Tick ()");

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual("Game.Player.Jump ()", frames[0].Symbol);
            Assert.IsNull(frames[0].File);
            Assert.AreEqual(0, frames[0].Line);
            Assert.AreEqual("Game.Loop.Tick ()", frames[1].Symbol);
            Assert.IsNull(frames[1].File);
            Assert.AreEqual(0, frames[1].Line);
        }

        [Test]
        public void Parses_dotnet_exception_traces()
        {
            var frames = UnityStackTrace.Parse(
                "  at Game.Player.Jump () [0x00012] in /src/Player.cs:42 \r\n  at Game.Loop.Tick () [0x00000] in <abcdef>:0 ");

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual("Game.Player.Jump ()", frames[0].Symbol);
            Assert.AreEqual("/src/Player.cs", frames[0].File);
            Assert.AreEqual(42, frames[0].Line);
            Assert.AreEqual("Game.Loop.Tick ()", frames[1].Symbol);
            Assert.IsNull(frames[1].File);
        }

        [Test]
        public void Empty_or_null_trace_gives_no_frames()
        {
            Assert.AreEqual(0, UnityStackTrace.Parse("").Count);
            Assert.AreEqual(0, UnityStackTrace.Parse(null).Count);
            Assert.AreEqual(0, UnityStackTrace.Parse("\n \r\n").Count);
        }

        [Test]
        public void Malformed_lines_keep_the_symbol_and_never_throw()
        {
            var frames = UnityStackTrace.Parse("A (at )\nB (at x.cs:)\nC (at :7)\n (at y.cs:3)\nD in \n---");

            Assert.AreEqual(new[] { "A", "B", "C", "(at y.cs:3)", "D in" }, Symbols(frames));
            foreach (var frame in frames)
            {
                Assert.IsNull(frame.File);
                Assert.AreEqual(0, frame.Line);
            }
        }

        private static string[] Symbols(System.Collections.Generic.IReadOnlyList<StackFrame> frames)
        {
            var symbols = new string[frames.Count];
            for (var i = 0; i < frames.Count; i++) symbols[i] = frames[i].Symbol;
            return symbols;
        }
    }
}
