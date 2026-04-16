using System.Threading.Tasks;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Debugger;

public interface IRunDebugger
{
    public static abstract IRunDebugger CreateNew();

    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure);
    public Task SignalStageBeginAsync(string sessionId, string name);
    public Task SignalStepBeginAsync(string sessionId, int stepId);
    public Task SignalStepResultChangeAsync(string sessionId, StepResultGeneric result);
    public Task SignalVariableUpdateAsync(string sessionId, string name, VariableState variable);
    public Task SignalArtifactUpdateAsync(string sessionId, string name, ArtifactState artifact);
    public Task SignalTimelineRunFinishedAsync(string sessionId);

    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId);
}