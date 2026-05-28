using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Sends timeline debug signals over the built-in named-pipe transport to an attached debugger UI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PipeRunDebugger : IRunDebugger, IDisposable
{
    private readonly PipeClient client = new(PipeTransport.GetPipeName());

    /// <summary>
    /// Signals that a timeline run has been initialized.
    /// </summary>
    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure)
    {
        return client.SignalAsync(new PipeInitTimelineRunSignal
        {
            SessionId = sessionId,
            Name = name,
            ProjectPath = projectPath,
            RunStructure = runStructure
        });
    }

    /// <summary>
    /// Signals that a runtime entity changed lifecycle state.
    /// </summary>
    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null)
    {
        return client.SignalAsync(new PipeEntityTransitionSignal
        {
            SessionId = sessionId,
            EntityKind = entityKind,
            Stage = stage,
            StepId = stepId,
            PreviousState = previousState,
            OutcomeState = outcomeState,
            State = state
        });
    }

    /// <summary>
    /// Signals that a debugger-visible value changed.
    /// </summary>
    public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
    {
        return client.SignalAsync(new PipeValueUpdateSignal
        {
            SessionId = sessionId,
            Name = name,
            ValueKind = valueKind,
            Stage = stage,
            StepId = stepId,
            Envelope = value
        });
    }

    /// <summary>
    /// Signals a structured log entry.
    /// </summary>
    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
    {
        return client.SignalAsync(new PipeLogEntrySignal
        {
            SessionId = sessionId,
            Entry = entry
        });
    }

    /// <summary>
    /// Signals a structured assertion result.
    /// </summary>
    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
    {
        return client.SignalAsync(new PipeAssertionSignal
        {
            SessionId = sessionId,
            Entry = entry
        });
    }

    /// <summary>
    /// Signals that the active run finished.
    /// </summary>
    public Task SignalTimelineRunFinishedAsync(string sessionId)
    {
        return client.SignalAsync(new PipeTimelineRunFinishedSignal
        {
            SessionId = sessionId
        });
    }

    /// <summary>
    /// Signals a breakpoint hit and waits until execution may continue.
    /// </summary>
    public async Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        await client.SignalAsync(new PipeBreakpointHitRequestSignal
        {
            SessionId = sessionId,
            Stage = stage,
            StepId = stepId
        });
        await client.WaitForAsync(PipeSignalKind.BreakpointHitContinue);
    }

    /// <summary>
    /// Disposes the underlying pipe connection.
    /// </summary>
    public void Dispose()
    {
        client.Dispose();
    }
}