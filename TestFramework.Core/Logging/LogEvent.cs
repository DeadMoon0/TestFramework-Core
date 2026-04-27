using System;
using System.Linq;

namespace TestFramework.Core.Logging;

/// <summary>
/// Represents a single log event that knows how to format itself for the line writer.
/// </summary>
public abstract class LogEvent
{
    /// <summary>
    /// Gets or sets the current indentation level used when formatting the event.
    /// </summary>
    public int CurrentIndentLevel { get; set; }

    /// <summary>
    /// Splits a string by common line break combinations.
    /// </summary>
    /// <param name="s">The string to split.</param>
    public string[] SpitStringByCommonLineBreaks(string s)
    {
        return s.Split(["\r\n", "\r", "\n", "\n\r"], System.StringSplitOptions.None);
    }

    /// <summary>
    /// Prefixes a line with the indentation represented by the current indent level.
    /// </summary>
    /// <param name="writer">The writer that supplies the indentation token.</param>
    /// <param name="line">The line to indent.</param>
    public string PrefixLineWithIndentLevel(LogLineWriter writer, string line)
    {
        return String.Join("", Enumerable.Repeat(writer.IndentLevelString, CurrentIndentLevel)) + line;
    }

    /// <summary>
    /// Formats the event to the provided log line writer.
    /// </summary>
    /// <param name="writer">The writer that receives the formatted output.</param>
    public abstract void FormatLogEvent(LogLineWriter writer);
}