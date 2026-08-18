using TestFramework.Core.Debugger;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class InformationLogEvent(string format, object[] args) : LogEvent
{
    /// <summary>
    /// The format string and its arguments, unformatted.
    /// </summary>
    /// <remarks>
    /// Something the test itself said. The arguments travel as values, so a consumer can read the number a test
    /// logged rather than find it inside a sentence.
    /// </remarks>
    public override DebugLogFacts? Describe() => DebugLogFacts.Positional(format, args);

    public override void FormatLogEvent(LogLineWriter writer)
    {
        string s = args.Length == 0 ? format : string.Format(format, args);
        string[] lines = SpitStringByCommonLineBreaks(s);
        foreach (var line in lines)
        {
            writer.WriteLine(PrefixLineWithIndentLevel(writer, line));
        }
    }
}