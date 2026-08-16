namespace TestFramework.Core.Debugger;

/// <summary>
/// Discriminates the debugger protocol messages carried by a <see cref="DebugEnvelope"/>.
/// </summary>
/// <remarks>
/// Values are part of the wire format: append new members, never renumber existing ones. The
/// discriminator is explicit rather than derived from a CLR type name on purpose — this decides how
/// to deserialize data arriving over a local socket, and letting a payload name its own type would
/// be a deserialization hole.
/// </remarks>
public enum PipeSignalKind : ushort
{
    /// <summary>A run, stage or step changed lifecycle state.</summary>
    EntityTransition,

    /// <summary>A run started, carrying its full structure.</summary>
    InitTimelineRun,

    /// <summary>A run reached its end. The last message of a session.</summary>
    TimelineRunFinished,

    /// <summary>A variable or artifact changed.</summary>
    ValueUpdate,

    /// <summary>A structured log entry was emitted inside a step.</summary>
    LogEntry,

    /// <summary>An assertion produced a result.</summary>
    Assertion,

    /// <summary>A step reached a breakpoint and is waiting to be released.</summary>
    BreakpointHitRequest,

    /// <summary>Consumer to producer: release a step waiting at a breakpoint.</summary>
    BreakpointHitContinue,

    /// <summary>Consumer to producer: stop this run cooperatively.</summary>
    CancelRun
}
