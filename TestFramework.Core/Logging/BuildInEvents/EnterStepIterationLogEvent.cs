using TestFramework.Core.Debugger;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStepIterationLogEvent(int iteration) : LogEvent
{
    /// <summary>
    /// Nothing. This event narrates the console: a retry is a lifecycle transition, which is already carried.
    /// </summary>
    public override DebugLogFacts? Describe() => null;

    public override void FormatLogEvent(LogLineWriter writer)
    {
        writer.WriteLine(PrefixLineWithIndentLevel(writer, "↻  Retry " + iteration));
    }
}
