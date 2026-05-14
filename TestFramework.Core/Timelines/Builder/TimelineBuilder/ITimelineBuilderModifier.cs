using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

/// <summary>
/// Extends <see cref="ITimelineBuilder"/> with typed per-step modifier verbs such as timeout, retry, naming, execution controls, and result bindings.
/// </summary>
/// <typeparam name="TStepResultContext">The result context type produced by the current step.</typeparam>
public interface ITimelineBuilderModifier<TStepResultContext> : ITimelineBuilder,
    ITimeOutModAction<TStepResultContext>,
    ISetupRetryModAction<TStepResultContext>,
    IExpectExceptionsModAction<TStepResultContext>,
    INameModAction<TStepResultContext>,
    IRunExclusivelyModAction<TStepResultContext>,
    IStepIOModAction<TStepResultContext>
    where TStepResultContext : StepResultContext;