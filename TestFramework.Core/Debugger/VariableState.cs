using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents the debugger-facing state snapshot of a variable.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record VariableState
{
    /// <summary>
    /// Gets the variable key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the common JSON-first value envelope.
    /// </summary>
    public required DebugValueEnvelope Envelope { get; init; }
}