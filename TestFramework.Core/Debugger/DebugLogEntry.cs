using System;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents a structured log entry transported through the run debugger protocol.
/// </summary>
public sealed class DebugLogEntry
{
    /// <summary>
    /// Gets or sets when the log entry occurred.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the log severity.
    /// </summary>
    public DebugLogLevel Level { get; set; } = DebugLogLevel.Information;

    /// <summary>
    /// Gets or sets the originating log event type name.
    /// </summary>
    public string EventName { get; set; } = "";

    /// <summary>
    /// Gets or sets the rendered message body.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Gets or sets the rendered message split into transport-safe lines.
    /// </summary>
    public string[] Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the indentation depth active when the entry was emitted.
    /// </summary>
    public int IndentLevel { get; set; }

    /// <summary>
    /// Gets or sets the active stage name when available.
    /// </summary>
    public string? Stage { get; set; }

    /// <summary>
    /// Gets or sets the active step identifier when available.
    /// </summary>
    public int? StepId { get; set; }

    /// <summary>
    /// Gets or sets the active step iteration number when available.
    /// </summary>
    public int? Iteration { get; set; }

    /// <summary>
    /// Gets or sets the active assertion scope descriptor when available.
    /// </summary>
    public string? AssertionScope { get; set; }
}