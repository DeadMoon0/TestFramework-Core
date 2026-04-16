using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IConditionalAction
{
    public ITimelineBuilder Conditional<TVar>(ImmutableVariable<TVar, bool> shouldRun, Action<ITimelineBuilder> steps) where TVar : VariableReference<bool>;
    public ITimelineBuilder Conditional(bool shouldRun, Action<ITimelineBuilder> steps);
}