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
        Publish(logEvent, InferLevel(logEvent));
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogInformation(string log)
    {
        Publish(new InformationLogEvent(log, []), DebugLogLevel.Information);
    }

    /// <summary>
    /// Logs a formatted informational message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogInformation(string format, params object[] args)
    {
        Publish(new InformationLogEvent(format, args), DebugLogLevel.Information);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogWarning(string log)
    {
        Publish(new WarningLogEvent(log, []), DebugLogLevel.Warning);
    }

    /// <summary>
    /// Logs a formatted warning message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogWarning(string format, params object[] args)
    {
        Publish(new WarningLogEvent(format, args), DebugLogLevel.Warning);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="log">The message to log.</param>
    public void LogError(string log)
    {
        Publish(new ErrorLogEvent(log, []), DebugLogLevel.Error);
    }

    /// <summary>
    /// Logs a formatted error message.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The format arguments.</param>
    public void LogError(string format, params object[] args)
    {
        Publish(new ErrorLogEvent(format, args), DebugLogLevel.Error);
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

    /// <summary>
    /// Hands an event to the session, which decides who wants what.
    /// </summary>
    /// <remarks>
    /// The event goes over whole rather than being rendered here. Rendering it at this point is what put the
    /// console's output onto every transport: whoever is displaying wants lines, whoever is recording wants the
    /// facts behind them, and only the session knows which of the two are attached.
    /// </remarks>
    private void Publish(LogEvent logEvent, DebugLogLevel level)
    {
        debuggingSession?.PublishLog(logEvent, level, indentLevel.Value, CurrentScope?.GetType().Name);
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

}