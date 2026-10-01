namespace TestFramework.Core.Debugger;

/// <summary>
/// A run starting, as a debugger is told about it.
/// </summary>
/// <remarks>
/// The debugger signals carry one entry each, the way <see cref="DebugLogEntry"/> and
/// <see cref="DebugAssertionEntry"/> already did, so a signal can carry more without changing a method every
/// debugger implements. The run's name and project are the identity's, so they are read from it rather than
/// passed beside it, where the two could disagree.
/// </remarks>
public sealed record DebugRunStart
{
    /// <summary>Gets which test the run belongs to.</summary>
    public required TestIdentity Identity { get; init; }

    /// <summary>Gets the run's stages and steps, as planned.</summary>
    public required TimelineRunStructure RunStructure { get; init; }
}

/// <summary>
/// A run, stage or step changing lifecycle state, as a debugger is told about it.
/// </summary>
public sealed record DebugEntityTransition
{
    /// <summary>Gets whether a run, a stage or a step changed.</summary>
    public required DebugEntityKind EntityKind { get; init; }

    /// <summary>Gets the stage it belongs to, or <see langword="null"/> for the run itself.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step, or <see langword="null"/> for a run or a stage.</summary>
    public int? StepId { get; init; }

    /// <summary>Gets the state it is in now.</summary>
    public required DebugLifecycleState State { get; init; }

    /// <summary>Gets the state it was in, when known.</summary>
    public DebugLifecycleState? PreviousState { get; init; }

    /// <summary>Gets how a finished step ended, when it has.</summary>
    public DebugLifecycleState? OutcomeState { get; init; }

    /// <summary>Gets what went wrong, for a step that failed.</summary>
    public DebugFailureDetail? Failure { get; init; }
}

/// <summary>
/// A variable or artifact the debugger shows changing value.
/// </summary>
public sealed record DebugValueUpdate
{
    /// <summary>Gets the variable's or artifact's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether it is a variable or an artifact.</summary>
    public required DebugValueKind ValueKind { get; init; }

    /// <summary>Gets the stage that changed it, when one was running.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step that changed it, when one was running.</summary>
    public int? StepId { get; init; }

    /// <summary>Gets the value, described for display.</summary>
    public required DebugValueEnvelope Value { get; init; }
}
