namespace TestFrameworkCore.Logging.BuildInEvents;

internal class ErrorLogEvent(string format, object[] args) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        string s = args.Length == 0 ? format : string.Format(format, args);
        string[] lines = SpitStringByCommonLineBreaks(s);
        const string prefix = "[ERROR]  ";
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