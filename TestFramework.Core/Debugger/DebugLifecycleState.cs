namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents a lifecycle state used by the debugger state-machine protocol.
/// </summary>
public enum DebugLifecycleState
{
    /// <summary>
    /// The entity has been created but not started.
    /// </summary>
    Initialized,

    /// <summary>
    /// The entity is actively executing.
    /// </summary>
    Running,

    /// <summary>
    /// The entity is waiting to be retried.
    /// </summary>
    WaitingForRetry,

    /// <summary>
    /// The entity completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// The entity completed with an error.
    /// </summary>
    Error,

    /// <summary>
    /// The entity timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// The entity was skipped.
    /// </summary>
    Skipped
}