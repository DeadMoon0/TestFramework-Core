using System;
using System.Linq;

namespace TestFramework.Core.Logging;

public abstract class LogEvent
{
    public int CurrentIndentLevel { get; set; }

    public string[] SpitStringByCommonLineBreaks(string s)
    {
        return s.Split(["\r\n", "\r", "\n", "\n\r"], System.StringSplitOptions.None);
    }

    public string PrefixLineWithIndentLevel(LogLineWriter writer, string line)
    {
        return String.Join("", Enumerable.Repeat(writer.IndentLevelString, CurrentIndentLevel)) + line;
    }

    public abstract void FormatLogEvent(LogLineWriter writer);
}