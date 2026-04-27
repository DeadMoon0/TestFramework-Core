using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents the debugger-facing structure of a stage.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugStageState
{
    /// <summary>
    /// Gets the stage name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the stage description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the steps contained in the stage.
    /// </summary>
    public required DebugStepState[] Steps { get; init; }
}