using System;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Describes a structured assertion result for debugger consumers.
/// </summary>
public sealed record DebugAssertionEntry
{
    /// <summary>
    /// Gets or sets when the assertion was evaluated.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; }
    /// <summary>
    /// Gets or sets the asserted subject category.
    /// </summary>
    public DebugAssertionTargetKind TargetKind { get; init; }
    /// <summary>
    /// Gets or sets the asserted subject display value.
    /// </summary>
    public string Target { get; init; } = "";
    /// <summary>
    /// Gets or sets the stable assertion method name.
    /// </summary>
    public string AssertionName { get; init; } = "";
    /// <summary>
    /// Gets or sets the display form of the assertion.
    /// </summary>
    public string AssertionDisplay { get; init; } = "";
    /// <summary>
    /// Gets or sets whether the assertion succeeded.
    /// </summary>
    public bool Succeeded { get; init; }
    /// <summary>
    /// Gets or sets the expected value or condition.
    /// </summary>
    public string Expected { get; init; } = "";
    /// <summary>
    /// Gets or sets the observed value or condition.
    /// </summary>
    public string Actual { get; init; } = "";
    /// <summary>
    /// Gets or sets the failure explanation when the assertion did not succeed.
    /// </summary>
    public string FailureReason { get; init; } = "";
    /// <summary>
    /// Gets or sets the active assertion scope name, when one is present.
    /// </summary>
    public string AssertionScope { get; init; } = "";
}