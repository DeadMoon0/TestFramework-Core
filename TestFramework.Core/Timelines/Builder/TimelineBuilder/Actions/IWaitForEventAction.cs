using TestFramework.Core.Events;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for waiting on an event step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IWaitForEventAction
{
    /// <summary>
    /// Adds an event step that waits until the event yields a result.
    /// </summary>
    public ITimelineBuilderModifier<TStepResultContext> WaitForEvent<TEvent, TStepResultContext>(Event<TEvent, TStepResultContext> sourceEvent)
        where TEvent : Event<TEvent, TStepResultContext>
        where TStepResultContext : StepResultContext;
}
