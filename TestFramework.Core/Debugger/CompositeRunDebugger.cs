using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

internal sealed class CompositeRunDebugger : IRunDebugger, ISupportsRunCancellation, ISupportsRenderedLog, ISupportsWidgets, ISupportsWidgetCaptureRequests
{
    /// <summary>
    /// Hands the run's capture handler to whichever child can be asked for evidence.
    /// </summary>
    /// <remarks>
    /// In practice exactly one can — a journal cannot be asked anything and neither can a console —
    /// so this looks like a fan-out and is really a search. Written as a fan-out anyway, because
    /// "the pipe is the only one" is a fact about today's debuggers rather than a rule, and a second
    /// live consumer would otherwise silently be the one that never gets asked. The getter answers
    /// from the first child holding one, which is the same handler every one of them was given.
    /// </remarks>
    Func<Task<WidgetCaptureOutcome>>? ISupportsWidgetCaptureRequests.OnCaptureRequested
    {
        get
        {
            foreach (IRunDebugger debugger in debuggers)
            {
                if (debugger is ISupportsWidgetCaptureRequests askable && askable.OnCaptureRequested is { } handler)
                    return handler;
            }

            return null;
        }
        set
        {
            foreach (IRunDebugger debugger in debuggers)
            {
                if (debugger is ISupportsWidgetCaptureRequests askable)
                    askable.OnCaptureRequested = value;
            }
        }
    }

    /// <summary>One interested consumer is enough to make producing the signals worthwhile.</summary>
    public bool IsCapturing => debuggers.Any(debugger => debugger.IsCapturing);

    private delegate Task DebuggerSignal(IRunDebugger debugger);

    private readonly IRunDebugger[] debuggers;

    /// <summary>
    /// Forwards a stop request from whichever consumers can carry one. Several may be attached, so
    /// the first to ask wins and the run stops once.
    /// </summary>
    public event Action<string?>? CancellationRequested
    {
        add
        {
            foreach (IRunDebugger debugger in debuggers)
            {
                if (debugger is ISupportsRunCancellation cancellable)
                    cancellable.CancellationRequested += value;
            }
        }
        remove
        {
            foreach (IRunDebugger debugger in debuggers)
            {
                if (debugger is ISupportsRunCancellation cancellable)
                    cancellable.CancellationRequested -= value;
            }
        }
    }

    public CompositeRunDebugger(params IRunDebugger[] debuggers)
    {
        this.debuggers = debuggers;
    }

    internal static IRunDebugger Create(params IRunDebugger[] debuggers)
    {
        return debuggers.Length switch
        {
            0 => new EmptyRunDebugger(),
            1 => debuggers[0],
            _ => new CompositeRunDebugger(debuggers)
        };
    }

    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        => SignalAllAsync(debugger => debugger.SignalInitTimelineRunAsync(sessionId, name, projectPath, runStructure, identity));

    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
        => SignalAllAsync(debugger => debugger.SignalEntityTransitionAsync(sessionId, entityKind, stage, stepId, state, previousState, outcomeState, failure));

    public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
        => SignalAllAsync(debugger => debugger.SignalValueUpdateAsync(sessionId, name, valueKind, stage, stepId, value));

    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
        => SignalAllAsync(debugger => debugger.SignalLogEntryAsync(sessionId, entry));

    /// <summary>
    /// Passes rendered lines to the children that display them, and to no others.
    /// </summary>
    /// <remarks>
    /// The same shape as the cancellation channel above: the capability is asked for rather than required, so a
    /// debugger that only serialises never sees a line of console output.
    /// </remarks>
    public void WriteRenderedLog(string[] lines, LogPlacement placement)
    {
        foreach (IRunDebugger debugger in debuggers)
        {
            if (debugger is ISupportsRenderedLog display)
                display.WriteRenderedLog(lines, placement);
        }
    }

    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        => SignalAllAsync(debugger => debugger.SignalAssertionAsync(sessionId, entry));

    /// <summary>
    /// Passes evidence to the children that can carry it, and to no others.
    /// </summary>
    /// <remarks>
    /// The same shape as the rendered log above: asked for rather than required, so a debugger built
    /// against a Core that had never heard of widgets keeps working and simply does not receive them.
    /// </remarks>
    public Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
    {
        List<Task> tasks = [];

        foreach (IRunDebugger debugger in debuggers)
        {
            if (debugger is ISupportsWidgets widgets)
                tasks.Add(widgets.SignalWidgetAsync(sessionId, entry));
        }

        return tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
    }

    public Task SignalTimelineRunFinishedAsync(string sessionId)
        => SignalAllAsync(debugger => debugger.SignalTimelineRunFinishedAsync(sessionId));

    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
        => SignalAllAsync(debugger => debugger.SignalAndWaitBreakpointHitAsync(sessionId, stage, stepId));

    private Task SignalAllAsync(DebuggerSignal signal)
    {
        Task[] tasks = new Task[debuggers.Length];
        for (int index = 0; index < debuggers.Length; index++)
            tasks[index] = signal(debuggers[index]);

        return Task.WhenAll(tasks);
    }
}