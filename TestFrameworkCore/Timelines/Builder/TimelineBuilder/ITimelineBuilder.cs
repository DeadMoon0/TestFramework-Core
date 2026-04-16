using TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder;

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