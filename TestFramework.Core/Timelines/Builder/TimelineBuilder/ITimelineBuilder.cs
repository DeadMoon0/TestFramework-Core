using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

/// <summary>
/// Exposes the consumer-first fluent API for defining a timeline before step modifiers are applied.
/// </summary>
public interface ITimelineBuilder :
    ISetVariableAction,
    IBuildAction,
    IRemoveArtifactAction,
    IRegisterArtifactAction,
    ITriggerAction,
    ISetupArtifactAction,
    IWaitForEventAction,
    ITransformAction,
    IAssertVariableAction,
    IConditionalAction,
    IVersionArtifactAction,
    IForEachAction,
    IFindArtifactAction;