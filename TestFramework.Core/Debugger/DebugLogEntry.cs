using System;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One log entry as it travels: the facts, and where in the run they were said.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is formatted. The entry used to carry the console's own output — the rendered message, the
/// same text a second time split into lines, and the indentation depth it was printed at — which meant every
/// consumer received a screenful of console instead of the data behind it, and could not recover the data.
/// </para>
/// <para>
/// What travels now is the template and its typed values. Rendering is the reader's, and
/// <see cref="DebugLogTemplate"/> is there for a reader that only wants the sentence.
/// </para>
/// <para>
/// The framework's narration of its own progress does not travel at all. Entering a step, a step's result, a
/// stage summary: those are lifecycle signals on the same transport, carrying the same facts in a form a
/// consumer can act on, and the run's plan already states each step's phase, label and retry policy.
/// </para>
/// </remarks>
public sealed record DebugLogEntry
{
    /// <summary>Gets when the entry was emitted.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Gets the severity.</summary>
    public DebugLogLevel Level { get; init; } = DebugLogLevel.Information;

    /// <summary>
    /// Gets the name of the log event type that emitted this.
    /// </summary>
    /// <remarks>
    /// A user-defined event keeps its own name here, which is how a consumer recognises a kind of entry it has
    /// been taught something about without having to match on the wording of its template.
    /// </remarks>
    public string EventName { get; init; } = "";

    /// <summary>Gets the sentence, with its holes unfilled.</summary>
    public string Template { get; init; } = "";

    /// <summary>Gets the values that fill the holes, typed as they were logged.</summary>
    public DebugLogField[] Fields { get; init; } = [];

    /// <summary>Gets the stage this was said in, when it was said inside one.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step this was said in, when it was said inside one.</summary>
    public int? StepId { get; init; }

    /// <summary>Gets which attempt of that step was running.</summary>
    public int? Iteration { get; init; }

    /// <summary>Gets the assertion scope that was open, when there was one.</summary>
    public string? AssertionScope { get; init; }
}
