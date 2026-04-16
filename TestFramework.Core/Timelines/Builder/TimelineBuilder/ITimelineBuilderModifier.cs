using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

public interface ITimelineBuilderModifier : ITimelineBuilder,
    ITimeOutModAction,
    ISetupRetryModAction,
    IExpectExceptionsModAction,
    INameModAction,
    IRunExclusivelyModAction;