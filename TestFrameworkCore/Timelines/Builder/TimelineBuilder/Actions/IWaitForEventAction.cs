using TestFrameworkCore.Events;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface IWaitForEventAction
{
    public ITimelineBuilderModifier WaitForEvent<TEvent, TResult>(Event<TEvent, TResult> sourceEvent) where TEvent : Event<TEvent, TResult>;
}
