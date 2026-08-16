using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Why a step ended in <see cref="DebugLifecycleState.Error"/> or <see cref="DebugLifecycleState.Timeout"/>.
/// </summary>
/// <remarks>
/// Without this a lifecycle transition says only that a step went red. The reason existed solely as
/// rendered log text, so a consumer had to scrape prose to explain a failure, and the framework's
/// own <see cref="TimelineFrameworkException.FriendlyMessage"/> / <see cref="TimelineFrameworkException.RecoverySteps"/>
/// guidance never reached a debugger at all.
/// </remarks>
public sealed record DebugFailureDetail
{
    /// <summary>Gets the CLR type name of the exception that ended the attempt.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Gets the exception message.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the framework's plain-language explanation, when the failure was a framework exception.
    /// </summary>
    public string? FriendlyMessage { get; init; }

    /// <summary>Gets the framework's suggested recovery steps, when available.</summary>
    public IReadOnlyList<string> RecoverySteps { get; init; } = [];

    /// <summary>Gets the alternatives the framework could offer, such as valid identifiers.</summary>
    public IReadOnlyList<string> AvailableOptions { get; init; } = [];

    /// <summary>Gets the stack trace, when the exception carried one.</summary>
    public string? StackTrace { get; init; }

    /// <summary>Gets the chain of inner exception types and messages, outermost first.</summary>
    public IReadOnlyList<string> InnerExceptions { get; init; } = [];

    /// <summary>Gets the attempt this failure belongs to, counting from one.</summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Gets a value indicating whether another attempt follows. A failure with a retry ahead of it
    /// reads very differently from a final one.
    /// </summary>
    public bool WillRetry { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>ErrorHandlingOptions.IgnoreExceptionTypes</c> absorbed the
    /// failure, so the step did not fail the run. Otherwise a suppressed error looks like a
    /// contradiction: an exception, and a step that passed.
    /// </summary>
    public bool WasSuppressed { get; init; }

    /// <summary>
    /// Captures an exception, pulling out the framework's guidance when there is any.
    /// </summary>
    internal static DebugFailureDetail? Capture(Exception? exception, int attempt, bool willRetry, bool wasSuppressed)
    {
        if (exception is null)
            return null;

        List<string> inner = [];
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
            inner.Add($"{current.GetType().FullName}: {current.Message}");

        TimelineFrameworkException? frameworkException = exception as TimelineFrameworkException;

        return new DebugFailureDetail
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            FriendlyMessage = frameworkException?.FriendlyMessage,
            RecoverySteps = frameworkException?.RecoverySteps ?? [],
            AvailableOptions = frameworkException?.AvailableOptions ?? [],
            StackTrace = exception.StackTrace,
            InnerExceptions = inner,
            Attempt = attempt,
            WillRetry = willRetry,
            WasSuppressed = wasSuppressed
        };
    }
}
