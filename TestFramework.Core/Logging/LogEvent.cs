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
    /// <remarks>
    /// This is the console's layout, and only the console's. What it writes is never transported: a rendered
    /// line cannot be turned back into the values behind it, and every consumer that was handed one had to
    /// parse a sentence to recover what it already knew.
    /// </remarks>
    /// <param name="writer">The writer that receives the formatted output.</param>
    public abstract void FormatLogEvent(LogLineWriter writer);

    /// <summary>
    /// States the facts behind the event, for the debug transport.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A template with its holes unfilled, and the values that fill them, typed as they were logged. A
    /// consumer can render the sentence, show the values as columns, or group entries by the template they
    /// share — none of which is possible once the values have been formatted into prose.
    /// </para>
    /// <para>
    /// Return <see langword="null"/> when the event only narrates the console. The framework's own progress —
    /// entering a stage, a step's result, the run's summary — is on the transport already, as lifecycle signals
    /// carrying the same facts in a form a consumer can act on. Saying it a second time in sentences made the
    /// log half of every journal and told nobody anything new.
    /// </para>
    /// </remarks>
    public abstract Debugger.DebugLogFacts? Describe();
}