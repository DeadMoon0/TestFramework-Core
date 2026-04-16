namespace TestFrameworkCore.Logging.BuildInEvents;

internal class EnterStepIterationLogEvent(int iteration) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        writer.WriteLine(PrefixLineWithIndentLevel(writer, "↻  Retry " + iteration));
    }
}
