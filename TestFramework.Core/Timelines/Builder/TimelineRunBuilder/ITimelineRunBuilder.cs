using TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder;

public interface ITimelineRunBuilder :
    IAddVariableAction,
    IAddArtifactAction,
    IRunAsyncAction;