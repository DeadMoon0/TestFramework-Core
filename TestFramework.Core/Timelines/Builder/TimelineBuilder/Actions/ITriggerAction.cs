using System.ComponentModel;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for appending a concrete step to the timeline.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITriggerAction
{
    /// <summary>
    /// Adds the provided step to the timeline.
    /// </summary>
    public ITimelineBuilderModifier<TStepResultContext> Trigger<TStepResultContext>(Step<TStepResultContext> triggerStep) where TStepResultContext : StepResultContext;
}