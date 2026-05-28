namespace TestFramework.Core.Debugger;

internal enum PipeSignalKind : ushort
{
    EntityTransition,
    InitTimelineRun,
    TimelineRunFinished,
    ValueUpdate,
    LogEntry,
    Assertion,
    BreakpointHitRequest,
    BreakpointHitContinue
}