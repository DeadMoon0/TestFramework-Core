using System;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

using System.ComponentModel;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for configuring step timeouts.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITimeoutModAction<TStepResultContext> : IImplementedByCore where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Sets the timeout for the current typed step.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> WithTimeout(VariableReference<TimeSpan> timeout);
}