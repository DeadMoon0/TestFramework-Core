using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

/// <summary>
/// Extends <see cref="ITimelineBuilder"/> with per-step modifier verbs such as timeout, retry, naming, and execution controls.
/// </summary>
public interface ITimelineBuilderModifier : ITimelineBuilder,
    ITimeOutModAction,
    ISetupRetryModAction,
    IExpectExceptionsModAction,
    INameModAction,
    IRunExclusivelyModAction;