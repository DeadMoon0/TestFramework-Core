using System;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for declaring expected exception types on a step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IExpectExceptionsModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Declares the exception types that are considered expected for the current typed step.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> ExpectExceptions(params Type[] exceptionTypes);
}