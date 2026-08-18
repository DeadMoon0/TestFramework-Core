using System.Collections.Generic;
using Xunit.Abstractions;

namespace TestFramework.Core.Logging;

/// <summary>
/// Renders a log event to lines, for the console.
/// </summary>
/// <remarks>
/// Here rather than in the debugger layer because the writer and the indentation token belong to logging. It
/// is called only when something is actually displaying: an event that nobody renders is an event nobody
/// should pay to format, and until now every event was formatted whether or not a console existed.
/// </remarks>
internal static class LogRendering
{
    /// <summary>The token one indentation level is written with.</summary>
    private const string Indent = "\t";

    /// <summary>Formats an event as the console would print it.</summary>
    internal static string[] ToLines(LogEvent logEvent, int indentLevel)
    {
        Collector collector = new();
        LogLineWriter writer = new(collector, Indent);

        logEvent.CurrentIndentLevel = indentLevel;
        logEvent.FormatLogEvent(writer);

        return [.. collector.Lines];
    }

    /// <summary>Catches what an event writes instead of putting it on a test's output.</summary>
    private sealed class Collector : ITestOutputHelper
    {
        internal List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
