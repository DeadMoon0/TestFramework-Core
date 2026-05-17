namespace TestFramework.Core.Debugger;

/// <summary>
/// Identifies the kind of debugger-visible value carried by a snapshot.
/// </summary>
public enum DebugValueKind
{
    /// <summary>
    /// A runtime variable value.
    /// </summary>
    Variable,

    /// <summary>
    /// A runtime artifact value.
    /// </summary>
    Artifact
}