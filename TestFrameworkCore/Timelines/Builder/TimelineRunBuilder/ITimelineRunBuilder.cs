using TestFrameworkCore.Timelines.Builder.TimelineRunBuilder.Actions;

namespace TestFrameworkCore.Timelines.Builder.TimelineRunBuilder;

public interface ITimelineRunBuilder :
    IAddVariableAction,
    IAddArtifactAction,
    IRunAsyncAction;