using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Sends timeline debug signals over the built-in named-pipe transport to an attached debugger UI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PipeRunDebugger : IRunDebugger, IDisposable, ISupportsRunCancellation, ISupportsWidgets, ISupportsWidgetCaptureRequests
{
    private readonly PipeClient client = new(PipeTransport.GetPipeName());

    /// <summary>
    /// What answers the UI when it asks for fresh evidence.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly because the socket is internal — a run installs its handler, and the
    /// public surface of a debugger is signals out, not requests in.
    /// </remarks>
    Func<Task<WidgetCaptureOutcome>>? ISupportsWidgetCaptureRequests.OnCaptureRequested
    {
        get => client.OnCaptureRequested;
        set => client.OnCaptureRequested = value;
    }

    /// <summary>
    /// Raised when the attached UI asks this run to stop.
    /// </summary>
    public event Action<string?>? CancellationRequested
    {
        add => client.CancellationRequested += value;
        remove => client.CancellationRequested -= value;
    }

    /// <summary>
    /// Gets a value indicating whether a debugger UI is attached, or might still be.
    /// </summary>
    /// <remarks>
    /// The connection is made lazily on the first signal, so before then the honest answer is
    /// "possibly": the transport is enabled and this process has not already found the pipe empty.
    /// </remarks>
    public bool IsCapturing => client.IsAttachedOrCouldAttach;

    /// <summary>
    /// Signals that a timeline run has been initialized.
    /// </summary>
    public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start)
    {
        return client.SignalAsync(new PipeInitTimelineRunSignal
        {
            SessionId = sessionId,
            Name = start.Identity.DisplayName,
            ProjectPath = start.Identity.AssemblyPath,
            RunStructure = start.RunStructure,
            Identity = start.Identity
        });
    }

    /// <summary>
    /// Signals that a runtime entity changed lifecycle state.
    /// </summary>
    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition)
    {
        return client.SignalAsync(new PipeEntityTransitionSignal
        {
            SessionId = sessionId,
            EntityKind = transition.EntityKind,
            Stage = transition.Stage,
            StepId = transition.StepId,
            PreviousState = transition.PreviousState,
            OutcomeState = transition.OutcomeState,
            State = transition.State,
            Failure = transition.Failure
        });
    }

    /// <summary>
    /// Signals that a debugger-visible value changed.
    /// </summary>
    public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update)
    {
        return client.SignalAsync(new PipeValueUpdateSignal
        {
            SessionId = sessionId,
            Name = update.Name,
            ValueKind = update.ValueKind,
            Stage = update.Stage,
            StepId = update.StepId,
            Envelope = update.Value
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
    /// Signals a piece of evidence a step or component produced.
    /// </summary>
    public Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
    {
        return client.SignalAsync(new PipeWidgetSignal
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
    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        // One call, so the answer cannot arrive before anything is listening for it. Sending and then
        // waiting are the same operation here, and splitting them is what made a step that was never
        // meant to pause wait out the whole timeout.
        return client.ExchangeAsync(
            new PipeBreakpointHitRequestSignal
            {
                SessionId = sessionId,
                Stage = stage,
                StepId = stepId
            },
            PipeSignalKind.BreakpointHitContinue);
    }

    /// <summary>
    /// Disposes the underlying pipe connection.
    /// </summary>
    public void Dispose()
    {
        client.Dispose();
    }
}