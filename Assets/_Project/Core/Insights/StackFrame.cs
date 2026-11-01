namespace Kturker.UnityBase.Core.Insights
{
    // One line of a C# stack: File is null and Line 0 when the trace has no source position (IL2CPP release builds)
    public readonly struct StackFrame
    {
        public StackFrame(string symbol, string file, int line)
        {
            Symbol = symbol;
            File = file;
            Line = line;
        }

        public string Symbol { get; }
        public string File { get; }
        public int Line { get; }
    }
}
