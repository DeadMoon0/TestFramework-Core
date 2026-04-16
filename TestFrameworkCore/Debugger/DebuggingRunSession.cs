using System;
using System.Threading.Tasks;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Steps;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Debugger;

internal class DebuggingRunSession(IRunDebugger debugger)
{
    internal string SessionId { get; } = Guid.NewGuid().ToString();
    internal IRunDebugger Debugger { get; set; } = debugger;

    internal async Task InitSessionAsync(TimelineRunStructure runStructure)
    {
        await Debugger.SignalInitTimelineRunAsync(SessionId, GetTestName(), GetProjectPath(), runStructure);
    }

    internal Task UpdateVariableAsync(VariableIdentifier identifier, VariableState state)
    {
        return Debugger.SignalVariableUpdateAsync(SessionId, identifier, state);
    }

    internal Task UpdateArtifactAsync(ArtifactIdentifier identifier, ArtifactState state)
    {
        return Debugger.SignalArtifactUpdateAsync(SessionId, identifier, state);
    }

    internal Task EnterStageAsync(string name)
    {
        return Debugger.SignalStageBeginAsync(SessionId, name);
    }

    internal Task EnterStepAsync(int index)
    {
        return Debugger.SignalStepBeginAsync(SessionId, index);
    }

    internal Task SetStepResultAsync(StepResultGeneric result)
    {
        return Debugger.SignalStepResultChangeAsync(SessionId, result);
    }

    internal Task FinishSessionAsync()
    {
        return Debugger.SignalTimelineRunFinishedAsync(SessionId);
    }

    internal async Task WaitWhenBreakpointHit(string stage, int index)
    {
        await Debugger.SignalAndWaitBreakpointHitAsync(SessionId, stage, index);
    }

    private static string GetTestName()
    {
        return "TempTestName"; //TODO: 
    }

    private static string GetProjectPath()
    {
        return "/TempTestProjectPath.csProj"; //TODO: 
    }
}