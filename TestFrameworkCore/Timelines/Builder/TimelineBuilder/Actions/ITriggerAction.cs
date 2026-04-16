using TestFrameworkCore.Steps;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ITriggerAction
{
    public ITimelineBuilderModifier Trigger<TResult>(Step<TResult> triggerStep);
}