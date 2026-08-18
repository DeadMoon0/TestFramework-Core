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
    /// <remarks>
    /// Prose, deliberately. This and the two lists below are not renderings of data carried elsewhere — they
    /// are the framework saying something a consumer could not work out for itself, and <c>ExceptionType</c>
    /// above is the stable key to switch on when a consumer wants to say it differently.
    /// </remarks>
    public string? FriendlyMessage { get; init; }

    /// <summary>Gets the framework's suggested recovery steps, when available.</summary>
    public IReadOnlyList<string> RecoverySteps { get; init; } = [];

    /// <summary>Gets the alternatives the framework could offer, such as valid identifiers.</summary>
    public IReadOnlyList<string> AvailableOptions { get; init; } = [];

    /// <summary>Gets the stack trace, when the exception carried one.</summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets the chain of inner exceptions, outermost first.
    /// </summary>
    /// <remarks>
    /// Two fields per link rather than one pre-joined <c>"Type: message"</c> string, so a consumer can show the
    /// types as a chain and the messages as text without splitting a string on the first colon — which is also
    /// the character most likely to appear inside the message.
    /// </remarks>
    public IReadOnlyList<DebugExceptionLink> InnerExceptions { get; init; } = [];

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

        List<DebugExceptionLink> inner = [];
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            inner.Add(new DebugExceptionLink
            {
                ExceptionType = current.GetType().FullName ?? current.GetType().Name,
                Message = current.Message
            });
        }

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

/// <summary>One exception inside another.</summary>
public sealed record DebugExceptionLink
{
    /// <summary>Gets the exception's type, fully qualified.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Gets its message.</summary>
    public required string Message { get; init; }
}
