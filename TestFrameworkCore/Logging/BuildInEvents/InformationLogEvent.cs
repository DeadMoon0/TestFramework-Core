namespace TestFrameworkCore.Logging.BuildInEvents;

internal class InformationLogEvent(string format, object[] args) : LogEvent
{
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