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
    /// Gets the causes underneath the failure, outermost first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two fields per link rather than one pre-joined <c>"Type: message"</c> string, so a consumer can show the
    /// types as a chain and the messages as text without splitting a string on the first colon — which is also
    /// the character most likely to appear inside the message.
    /// </para>
    /// <para>
    /// A flattened tree rather than a chain, because causes are not always a chain. An
    /// <see cref="AggregateException"/> has many of them, and following <see cref="Exception.InnerException"/>
    /// alone reports the first and silently drops the rest — so a step that fanned out to twelve shards and had
    /// four of them fail explained one failure and hid three. Each link carries its
    /// <see cref="DebugExceptionLink.Depth"/> so the shape survives the flattening.
    /// </para>
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

        // The failing exception can itself be an aggregate — a fan-out step's own failure usually is. Starting
        // from its InnerException would take the first of its causes and quietly leave the others out, which is
        // the same loss this walk exists to prevent, one level higher up.
        if (exception is AggregateException aggregated)
        {
            foreach (Exception cause in aggregated.InnerExceptions)
                Collect(cause, depth: 0, inner);
        }
        else
        {
            Collect(exception.InnerException, depth: 0, inner);
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

    /// <summary>
    /// How many causes are recorded before the walk gives up.
    /// </summary>
    /// <remarks>
    /// An <see cref="AggregateException"/> from a wide fan-out can hold hundreds, and a consumer that has been
    /// handed two dozen reasons has been told everything it can act on. Deeper or wider than this and the rest
    /// are not recorded — the stack trace is still whole, and it is the thing to read when the causes run out.
    /// </remarks>
    private const int MostCausesRecorded = 24;

    /// <summary>
    /// Flattens the causes under an exception, outermost first, keeping how deep each one sat.
    /// </summary>
    /// <remarks>
    /// Depth-first and in order, so reading the list top to bottom reads the failure the way it happened: a cause,
    /// then what caused that, and — where something aggregated several — each of them in turn at the same depth.
    /// </remarks>
    private static void Collect(Exception? exception, int depth, List<DebugExceptionLink> into)
    {
        if (exception is null || into.Count >= MostCausesRecorded)
            return;

        into.Add(new DebugExceptionLink
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            Depth = depth
        });

        // Asked before InnerException, because an AggregateException's InnerException is only the first of its
        // InnerExceptions — following it would report one cause and lose the others without saying so.
        if (exception is AggregateException aggregate)
        {
            foreach (Exception cause in aggregate.InnerExceptions)
                Collect(cause, depth + 1, into);

            return;
        }

        Collect(exception.InnerException, depth + 1, into);
    }
}

/// <summary>One cause underneath a failure.</summary>
public sealed record DebugExceptionLink
{
    /// <summary>Gets the exception's type, fully qualified.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Gets its message.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets how far under the failure this cause sat, counting the first cause as zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Carried because the causes are a flattened tree, not a chain: two exceptions aggregated by the same parent
    /// are siblings at one depth, and a consumer indenting by list position would draw the second as nested inside
    /// the first — a claim about the failure that is simply untrue.
    /// </para>
    /// <para>
    /// Added without a protocol version behind it, deliberately. A recording made before it existed has no depths,
    /// which reads as zero and draws the causes as a flat list — losing the nesting, which is what those
    /// recordings knew anyway. Nothing changes meaning; a field appears.
    /// </para>
    /// </remarks>
    public int Depth { get; init; }
}
