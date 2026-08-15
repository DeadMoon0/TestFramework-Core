using System;
using System.Collections.Generic;
using System.Threading;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Logging.BuildInEvents;
using Xunit.Abstractions;

namespace TestFramework.Core.Logging;

/// <summary>
/// Provides structured, indentation-aware logging for timeline execution and assertions.
/// </summary>
public class ScopedLogger
{
    private readonly AsyncLocal<int> indentLevel = new();
    private readonly AsyncLocal<AssertionScope?> assertionScope = new();
    private readonly DebuggingRunSession? debuggingSession;

    internal static ScopedLogger CreateWithDebuggerSession(DebuggingRunSession debuggingSession) => new(debuggingSession, true);

    internal void SetAssertionScope(AssertionScope scope) => assertionScope.Value = scope;
    internal void ClearAssertionScope() => assertionScope.Value = null;
    internal AssertionScope? CurrentScope => assertionScope.Value;

    internal ScopedLogger(ITestOutputHelper? outputHelper)
    {
        debuggingSession = outputHelper is null
            ? null
            : new DebuggingRunSession(new OutputRunDebugger(outputHelper));
    }

    private ScopedLogger(DebuggingRunSession debuggingSession, bool _)
    {
        this.debuggingSession = debuggingSession;
    }

    /// <summary>
    /// Enters a deeper indentation scope and returns a disposable that restores the previous level.
    /// </summary>
    public LogScopeDisposable EnterIndentScope()
    {
        indentLevel.Value = indentLevel.Value + 1;
        return new LogScopeDisposable(this);
    }

    /// <summary>
    /// Exits one indentation scope level.
    /// </summary>
    public void ExitIndentScope()
    {
        indentLevel.Value = Math.Max(0, indentLevel.Value - 1);
    }

    /// <summary>
    /// Logs a preformatted log event.
    /// </summary>
    /// <param name="logEvent">The event to format and write.</param>
    public void Log(LogEvent logEvent)
    {
        SendEntry(CreateEntry(logEvent, InferLevel(logEvent)));
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogInformation(string log)
    {
        SendEntry(CreateEntry(new InformationLogEvent(log, []), DebugLogLevel.Information));
    }

    /// <summary>
    /// Logs a formatted informational message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogInformation(string format, params object[] args)
    {
        SendEntry(CreateEntry(new InformationLogEvent(format, args), DebugLogLevel.Information));
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogWarning(string log)
    {
        SendEntry(CreateEntry(new WarningLogEvent(log, []), DebugLogLevel.Warning));
    }

    /// <summary>
    /// Logs a formatted warning message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogWarning(string format, params object[] args)
    {
        SendEntry(CreateEntry(new WarningLogEvent(format, args), DebugLogLevel.Warning));
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogError(string log)
    {
        SendEntry(CreateEntry(new ErrorLogEvent(log, []), DebugLogLevel.Error));
    }

    /// <summary>
    /// Logs a formatted error message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogError(string format, params object[] args)
    {
        SendEntry(CreateEntry(new ErrorLogEvent(format, args), DebugLogLevel.Error));
    }

    internal void SignalAssertion(DebugAssertionTargetKind targetKind, string target, string assertionName, string assertionDisplay, bool succeeded, string expected = "", string actual = "", string failureReason = "")
    {
        // Queued rather than awaited: blocking the step thread on the whole debugger fan-out — which
        // includes a named-pipe write — used to cost real time on every single assertion.
        debuggingSession?.PublishAssertion(new DebugAssertionEntry
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TargetKind = targetKind,
            Target = target,
            AssertionName = assertionName,
            AssertionDisplay = assertionDisplay,
            Succeeded = succeeded,
            Expected = expected,
            Actual = actual,
            FailureReason = failureReason,
            AssertionScope = CurrentScope?.GetType().Name ?? ""
        });
    }

    private void SendEntry(DebugLogEntry entry)
    {
        debuggingSession?.PublishLog(entry);
    }

    private DebugLogEntry CreateEntry(LogEvent logEvent, DebugLogLevel level)
    {
        CollectingOutputHelper collector = new();
        LogLineWriter writer = new(collector, "\t");
        logEvent.CurrentIndentLevel = indentLevel.Value;
        logEvent.FormatLogEvent(writer);

        return new DebugLogEntry
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Level = level,
            EventName = logEvent.GetType().Name,
            Message = string.Join(System.Environment.NewLine, collector.Lines),
            Lines = [.. collector.Lines],
            IndentLevel = indentLevel.Value,
            AssertionScope = CurrentScope?.GetType().Name
        };
    }

    private static DebugLogLevel InferLevel(LogEvent logEvent)
    {
        return logEvent switch
        {
            WarningLogEvent => DebugLogLevel.Warning,
            ErrorLogEvent => DebugLogLevel.Error,
            _ => DebugLogLevel.Information
        };
    }

    private sealed class CollectingOutputHelper : ITestOutputHelper
    {
        internal List<string> Lines { get; } = [];

        public void WriteLine(string message)
        {
            Lines.Add(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            Lines.Add(string.Format(format, args));
        }
    }
}