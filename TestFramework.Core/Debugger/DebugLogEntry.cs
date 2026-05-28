using System;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents a structured log entry transported through the run debugger protocol.
/// </summary>
public sealed record DebugLogEntry
{
    /// <summary>
    /// Gets or sets when the log entry occurred.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the log severity.
    /// </summary>
    public DebugLogLevel Level { get; init; } = DebugLogLevel.Information;

    /// <summary>
    /// Gets or sets the originating log event type name.
    /// </summary>
    public string EventName { get; init; } = "";

    /// <summary>
    /// Gets or sets the rendered message body.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Gets or sets the rendered message split into transport-safe lines.
    /// </summary>
    public string[] Lines { get; init; } = [];

    /// <summary>
    /// Gets or sets the indentation depth active when the entry was emitted.
    /// </summary>
    public int IndentLevel { get; init; }

    /// <summary>
    /// Gets or sets the active stage name when available.
    /// </summary>
    public string? Stage { get; init; }

    /// <summary>
    /// Gets or sets the active step identifier when available.
    /// </summary>
    public int? StepId { get; init; }

    /// <summary>
    /// Gets or sets the active step iteration number when available.
    /// </summary>
    public int? Iteration { get; init; }

    /// <summary>
    /// Gets or sets the active assertion scope descriptor when available.
    /// </summary>
    public string? AssertionScope { get; init; }
}