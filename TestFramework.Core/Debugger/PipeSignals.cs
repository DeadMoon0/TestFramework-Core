using System;
using Newtonsoft.Json;

namespace TestFramework.Core.Debugger;

/// <summary>
/// A single debugger protocol message.
/// </summary>
/// <remarks>
/// Public so a consumer can be built against Core's package instead of reaching into its internals.
/// Most messages travel producer to consumer; <see cref="PipeBreakpointHitContinueSignal"/> and
/// <see cref="PipeCancelRunSignal"/> travel the other way.
/// </remarks>
public interface IPipeSignal
{
    /// <summary>Gets the discriminator used to deserialize this message.</summary>
    PipeSignalKind Kind { get; }

    /// <summary>
    /// Gets the run this signal belongs to.
    /// </summary>
    /// <remarks>
    /// Lifted onto the interface so an envelope header can be filled without deserializing the
    /// payload, and so signals can be routed once several runs share one debugger host.
    /// </remarks>
    string SessionId { get; }
}

/// <summary>Announces a run and describes its full structure.</summary>
public sealed record PipeInitTimelineRunSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.InitTimelineRun;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets the run's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the assembly or host path that identifies the run.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Gets the stages, steps, variables and artifacts the run starts with.</summary>
    public required TimelineRunStructure RunStructure { get; init; }

    /// <summary>
    /// Gets the test that started the run, precisely enough for a consumer to locate it in source
    /// and to build a re-run filter.
    /// </summary>
    public TestIdentity? Identity { get; init; }
}

/// <summary>Reports that a run, stage or step changed lifecycle state.</summary>
public sealed record PipeEntityTransitionSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.EntityTransition;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets which kind of entity moved.</summary>
    public required DebugEntityKind EntityKind { get; init; }

    /// <summary>Gets the owning stage, when the entity is a stage or step.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step index, when the entity is a step.</summary>
    public int? StepId { get; init; }

    /// <summary>Gets the state being left, when known.</summary>
    public DebugLifecycleState? PreviousState { get; init; }

    /// <summary>
    /// Gets the result of the attempt that just finished.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="State"/>: a retrying step is in
    /// <see cref="DebugLifecycleState.WaitingForRetry"/> while its last attempt ended in
    /// <see cref="DebugLifecycleState.Error"/>.
    /// </remarks>
    public DebugLifecycleState? OutcomeState { get; init; }

    /// <summary>Gets the state being entered.</summary>
    public required DebugLifecycleState State { get; init; }

    /// <summary>
    /// Gets the failure that ended the attempt, when the transition carries one.
    /// </summary>
    /// <remarks>
    /// Present so a consumer can explain <em>why</em> a step went red instead of only that it did.
    /// </remarks>
    public DebugFailureDetail? Failure { get; init; }

    /// <summary>Gets when the transition happened.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Reports that a variable or artifact changed.</summary>
public sealed record PipeValueUpdateSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.ValueUpdate;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets the variable or artifact identifier.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether this update describes a variable or an artifact.</summary>
    public required DebugValueKind ValueKind { get; init; }

    /// <summary>Gets the stage that produced the change, when attributable to a step.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step that produced the change, when attributable to one.</summary>
    public int? StepId { get; init; }

    /// <summary>Gets the value, with its display text and renderer schema key.</summary>
    public required DebugValueEnvelope Envelope { get; init; }

    /// <summary>Gets when the change was observed.</summary>
    public DateTimeOffset ObservedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Carries a structured log entry emitted inside a step.</summary>
public sealed record PipeLogEntrySignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.LogEntry;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets the log entry.</summary>
    public required DebugLogEntry Entry { get; init; }
}

/// <summary>Carries a structured assertion result.</summary>
public sealed record PipeAssertionSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.Assertion;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets the assertion result.</summary>
    public required DebugAssertionEntry Entry { get; init; }
}

/// <summary>Reports that a step reached a breakpoint and is waiting to be released.</summary>
public sealed record PipeBreakpointHitRequestSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.BreakpointHitRequest;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets the stage containing the paused step.</summary>
    public required string Stage { get; init; }

    /// <summary>Gets the index of the paused step.</summary>
    public required int StepId { get; init; }
}

/// <summary>Releases a step waiting at a breakpoint. Consumer to producer.</summary>
public sealed record PipeBreakpointHitContinueSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.BreakpointHitContinue;

    /// <summary>
    /// Gets the run to release.
    /// </summary>
    /// <remarks>
    /// Defaulted rather than required: today the continue reply travels back down the one connection
    /// that asked, so the sender need not name the session.
    /// </remarks>
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>Reports that a run finished. The last message of a session.</summary>
public sealed record PipeTimelineRunFinishedSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.TimelineRunFinished;

    /// <inheritdoc />
    public required string SessionId { get; init; }
}

/// <summary>
/// Asks a run to stop. Consumer to producer.
/// </summary>
/// <remarks>
/// A run is stopped by telling it to stop, never by killing its process: the timeline has to unwind
/// through its Cleanup stage so artifacts are deconstructed and environment components torn down.
/// Terminating the host skips all of that and strands containers, temp files and database rows.
/// </remarks>
public sealed record PipeCancelRunSignal : IPipeSignal
{
    /// <inheritdoc />
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.CancelRun;

    /// <inheritdoc />
    public required string SessionId { get; init; }

    /// <summary>Gets an optional human-readable reason, surfaced in the run's log.</summary>
    public string? Reason { get; init; }
}
