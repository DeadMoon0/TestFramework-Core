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
    /// Gets the CLR type name of the variable value.
    /// </summary>
    public required string TypeName { get; init; }
    /// <summary>
    /// Gets the serialized variable value.
    /// </summary>
    public required string Value { get; init; }
}