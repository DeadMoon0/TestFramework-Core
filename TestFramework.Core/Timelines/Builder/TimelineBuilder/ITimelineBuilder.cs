using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

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