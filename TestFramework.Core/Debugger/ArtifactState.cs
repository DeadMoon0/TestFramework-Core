using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents the debugger-facing state snapshot of an artifact.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record ArtifactState
{
    /// <summary>
    /// Gets the artifact key.
    /// </summary>
    public required string Key { get; init; }
    /// <summary>
    /// Gets the artifact kind name.
    /// </summary>
    public required string KindName { get; init; }
    /// <summary>
    /// Gets the serialized artifact reference.
    /// </summary>
    public required string Reference { get; init; }
    /// <summary>
    /// Gets the serialized artifact data.
    /// </summary>
    public required string Data { get; init; }
}