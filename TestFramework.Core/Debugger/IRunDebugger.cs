using System.ComponentModel;
using System.Threading.Tasks;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Defines the integration contract used by debugger frontends to observe timeline execution.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRunDebugger
{
    /// <summary>
    /// Creates a new debugger instance.
    /// </summary>
    public static abstract IRunDebugger CreateNew();

    /// <summary>
    /// Signals that a timeline run has been initialized.
    /// </summary>
    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure);
    /// <summary>
    /// Signals that a stage has begun.
    /// </summary>
    public Task SignalStageBeginAsync(string sessionId, string name);
    /// <summary>
    /// Signals that a step has begun.
    /// </summary>
    public Task SignalStepBeginAsync(string sessionId, int stepId);
    /// <summary>
    /// Signals that a step result has changed.
    /// </summary>
    public Task SignalStepResultChangeAsync(string sessionId, StepResultGeneric result);
    /// <summary>
    /// Signals that a variable state has changed.
    /// </summary>
    public Task SignalVariableUpdateAsync(string sessionId, string name, VariableState variable);
    /// <summary>
    /// Signals that an artifact state has changed.
    /// </summary>
    public Task SignalArtifactUpdateAsync(string sessionId, string name, ArtifactState artifact);
    /// <summary>
    /// Signals that the timeline run has finished.
    /// </summary>
    public Task SignalTimelineRunFinishedAsync(string sessionId);

    /// <summary>
    /// Signals a breakpoint hit and waits until execution may continue.
    /// </summary>
    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId);
}