using TestFramework.Core.Steps;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ITriggerAction
{
    public ITimelineBuilderModifier Trigger<TResult>(Step<TResult> triggerStep);
}