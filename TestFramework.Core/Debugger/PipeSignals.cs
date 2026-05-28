using System;
using Newtonsoft.Json;

namespace TestFramework.Core.Debugger;

internal interface IPipeSignal
{
    PipeSignalKind Kind { get; }
}

internal sealed record PipeInitTimelineRunSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.InitTimelineRun;

    public required string SessionId { get; init; }
    public required string Name { get; init; }
    public required string ProjectPath { get; init; }
    public required TimelineRunStructure RunStructure { get; init; }
}

internal sealed record PipeEntityTransitionSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.EntityTransition;

    public required string SessionId { get; init; }
    public required DebugEntityKind EntityKind { get; init; }
    public string? Stage { get; init; }
    public int? StepId { get; init; }
    public DebugLifecycleState? PreviousState { get; init; }
    public DebugLifecycleState? OutcomeState { get; init; }
    public required DebugLifecycleState State { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record PipeValueUpdateSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.ValueUpdate;

    public required string SessionId { get; init; }
    public required string Name { get; init; }
    public required DebugValueKind ValueKind { get; init; }
    public string? Stage { get; init; }
    public int? StepId { get; init; }
    public required DebugValueEnvelope Envelope { get; init; }
    public DateTimeOffset ObservedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record PipeLogEntrySignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.LogEntry;

    public required string SessionId { get; init; }
    public required DebugLogEntry Entry { get; init; }
}

internal sealed record PipeAssertionSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.Assertion;

    public required string SessionId { get; init; }
    public required DebugAssertionEntry Entry { get; init; }
}

internal sealed record PipeBreakpointHitRequestSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.BreakpointHitRequest;

    public required string SessionId { get; init; }
    public required string Stage { get; init; }
    public required int StepId { get; init; }
}

internal sealed record PipeBreakpointHitContinueSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.BreakpointHitContinue;
}

internal sealed record PipeTimelineRunFinishedSignal : IPipeSignal
{
    [JsonProperty]
    public PipeSignalKind Kind => PipeSignalKind.TimelineRunFinished;

    public required string SessionId { get; init; }
}