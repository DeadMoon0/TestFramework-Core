using System.Threading.Tasks;
using TestFrameworkCore.Steps;

namespace TestFrameworkCore.Debugger;

internal class EmptyRunDebugger : IRunDebugger
{
    public static IRunDebugger CreateNew() => new EmptyRunDebugger();

    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        return Task.CompletedTask;
    }

    public Task SignalArtifactUpdateAsync(string sessionId, string name, ArtifactState artifact)
    {
        return Task.CompletedTask;
    }

    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure)
    {
        return Task.CompletedTask;
    }

    public Task SignalStageBeginAsync(string sessionId, string name)
    {
        return Task.CompletedTask;
    }

    public Task SignalStepBeginAsync(string sessionId, int stepId)
    {
        return Task.CompletedTask;
    }

    public Task SignalStepResultChangeAsync(string sessionId, StepResultGeneric result)
    {
        return Task.CompletedTask;
    }

    public Task SignalTimelineRunFinishedAsync(string sessionId)
    {
        return Task.CompletedTask;
    }

    public Task SignalVariableUpdateAsync(string sessionId, string name, VariableState variable)
    {
        return Task.CompletedTask;
    }
}
