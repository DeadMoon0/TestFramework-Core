using System.ComponentModel;
using TestFramework.Core.Steps;

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
    public ITimelineBuilderModifier Trigger<TResult>(Step<TResult> triggerStep);
}