using System.Threading.Tasks;
namespace TestFramework.Core.Debugger;

internal class EmptyRunDebugger : IRunDebugger
{
    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        return Task.CompletedTask;
    }

    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null)
    {
        return Task.CompletedTask;
    }

    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure)
    {
        return Task.CompletedTask;
    }

    public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
    {
        return Task.CompletedTask;
    }

    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
    {
        return Task.CompletedTask;
    }

    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
    {
        return Task.CompletedTask;
    }

    public Task SignalTimelineRunFinishedAsync(string sessionId)
    {
        return Task.CompletedTask;
    }

}
