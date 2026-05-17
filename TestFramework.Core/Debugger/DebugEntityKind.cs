namespace TestFramework.Core.Debugger;

/// <summary>
/// Identifies which runtime entity changed state in the debugger protocol.
/// </summary>
public enum DebugEntityKind
{
    /// <summary>
    /// The full timeline run.
    /// </summary>
    Run,

    /// <summary>
    /// A stage inside the timeline run.
    /// </summary>
    Stage,

    /// <summary>
    /// A step inside a stage.
    /// </summary>
    Step
}