namespace TestFramework.Core.Debugger;

/// <summary>
/// Identifies the asserted subject category.
/// </summary>
public enum DebugAssertionTargetKind
{
    /// <summary>
    /// A direct value assertion.
    /// </summary>
    Value,
    /// <summary>
    /// A variable lookup assertion.
    /// </summary>
    Variable,
    /// <summary>
    /// An artifact assertion.
    /// </summary>
    Artifact,
    /// <summary>
    /// A single step assertion.
    /// </summary>
    Step,
    /// <summary>
    /// A step collection assertion.
    /// </summary>
    StepList
}