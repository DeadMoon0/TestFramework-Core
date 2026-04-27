using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Logging.BuildInEvents;
using Xunit.Abstractions;

namespace TestFramework.Core.Logging;

/// <summary>
/// Provides structured, indentation-aware logging for timeline execution and assertions.
/// </summary>
public class ScopedLogger
{
    int indentLevel = 0;
    LogLineWriter writer;
    bool _assertHeaderPrinted = false;
    AssertionScope? _assertionScope = null;

    internal void SetAssertionScope(AssertionScope scope) => _assertionScope = scope;
    internal void ClearAssertionScope() => _assertionScope = null;
    internal AssertionScope? CurrentScope => _assertionScope;

    internal ScopedLogger(ITestOutputHelper? outputHelper)
    {
        writer = new LogLineWriter(outputHelper, "\t");
    }

    /// <summary>
    /// Enters a deeper indentation scope and returns a disposable that restores the previous level.
    /// </summary>
    public LogScopeDisposable EnterIndentScope()
    {
        this.indentLevel++;
        return new LogScopeDisposable(this);
    }

    /// <summary>
    /// Exits one indentation scope level.
    /// </summary>
    public void ExitIndentScope()
    {
        this.indentLevel--;
    }

    internal void StartBuffering() => writer.StartBuffering();
    internal void StopBuffering() => writer.StopBuffering();
    internal void FlushBuffer() => writer.FlushBuffer();

    /// <summary>
    /// Logs a preformatted log event.
    /// </summary>
    /// <param name="logEvent">The event to format and write.</param>
    public void Log(LogEvent logEvent)
    {
        logEvent.CurrentIndentLevel = indentLevel;
        logEvent.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogInformation(string log)
    {
        new InformationLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs a formatted informational message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogInformation(string format, params object[] args)
    {
        new InformationLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogWarning(string log)
    {
        new WarningLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs a formatted warning message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogWarning(string format, params object[] args)
    {
        new WarningLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogError(string log)
    {
        new ErrorLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    /// <summary>
    /// Logs a formatted error message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogError(string format, params object[] args)
    {
        new ErrorLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    internal void EnsureAssertionHeaderPrinted()
    {
        if (_assertHeaderPrinted) return;
        _assertHeaderPrinted = true;
        LogInformation("");
        LogInformation("─────────────────────────────────────────────");
    }
}