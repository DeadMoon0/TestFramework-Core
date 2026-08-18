using TestFramework.Core.Debugger;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class WarningLogEvent(string format, object[] args) : LogEvent
{
    /// <summary>
    /// The format string and its arguments, unformatted.
    /// </summary>
    /// <remarks>
    /// Something the test or the framework wanted noticed. The arguments travel as values, so a consumer can read the number a test
    /// logged rather than find it inside a sentence.
    /// </remarks>
    public override DebugLogFacts? Describe() => DebugLogFacts.Positional(format, args);

    public override void FormatLogEvent(LogLineWriter writer)
    {
        string s = args.Length == 0 ? format : string.Format(format, args);
        string[] lines = SpitStringByCommonLineBreaks(s);
        const string prefix = "[WARN]   ";
        const string continuation = "         ";
        bool first = true;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            writer.WriteLine(PrefixLineWithIndentLevel(writer, (first ? prefix : continuation) + line));
            first = false;
        }
    }
}