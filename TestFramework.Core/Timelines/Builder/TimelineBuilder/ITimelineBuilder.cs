using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

/// <summary>
/// Exposes the consumer-first fluent API for defining a timeline.
/// Each step-producing call returns a typed modifier so the next fluent call can immediately add options such as <c>Name(...)</c>, <c>WithRetry(...)</c>, or result bindings before continuing.
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