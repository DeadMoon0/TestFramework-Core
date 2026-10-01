using System.Threading.Tasks;
namespace TestFramework.Core.Debugger;

internal class EmptyRunDebugger : IRunDebugger
{
    /// <summary>Nothing is listening, so nothing needs to be produced.</summary>
    public bool IsCapturing => false;

    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        return Task.CompletedTask;
    }

    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition)
    {
        return Task.CompletedTask;
    }

    public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start)
    {
        return Task.CompletedTask;
    }

    public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update)
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
