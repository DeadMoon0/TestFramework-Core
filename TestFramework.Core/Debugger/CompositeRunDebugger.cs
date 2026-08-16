using System;
using System.Linq;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

internal sealed class CompositeRunDebugger : IRunDebugger, ISupportsRunCancellation
{
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

    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        => SignalAllAsync(debugger => debugger.SignalAssertionAsync(sessionId, entry));

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