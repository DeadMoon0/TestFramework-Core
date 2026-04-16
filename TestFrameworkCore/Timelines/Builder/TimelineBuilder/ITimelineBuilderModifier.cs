using TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder;

public interface ITimelineBuilderModifier : ITimelineBuilder,
    ITimeOutModAction,
    ISetupRetryModAction,
    IExpectExceptionsModAction,
    INameModAction,
    IRunExclusivelyModAction;