using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Defines the integration contract used by debugger frontends to observe timeline execution.
/// Register implementations through dependency injection to mirror timeline runs into custom tools.
/// </summary>
public interface IRunDebugger
{
    /// <summary>
    /// Gets a value indicating whether this debugger will do anything with the signals it is sent.
    /// </summary>
    /// <remarks>
    /// Producing a signal is not free — values are formatted, JSON is built, stack traces are walked.
    /// A debugger that reports <see langword="false"/> lets the framework skip that work entirely.
    /// The default is <see langword="true"/>, which is the safe answer for any implementation that
    /// does not opt out.
    /// </remarks>
    public bool IsCapturing => true;

    /// <summary>
    /// Signals that a timeline run has been initialized.
    /// </summary>
    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure);
    /// <summary>
    /// Signals that a runtime entity has transitioned to a new lifecycle state.
    /// </summary>
    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null);
    /// <summary>
    /// Signals that a debugger-visible value has changed.
    /// </summary>
    public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value);
    /// <summary>
    /// Signals that a structured log entry has been emitted for the active run.
    /// </summary>
    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry);
    /// <summary>
    /// Signals a structured assertion result.
    /// </summary>
    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry);
    /// <summary>
    /// Signals that the timeline run has finished.
    /// </summary>
    public Task SignalTimelineRunFinishedAsync(string sessionId);

    /// <summary>
    /// Signals a breakpoint hit and waits until execution may continue.
    /// </summary>
    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId);
}