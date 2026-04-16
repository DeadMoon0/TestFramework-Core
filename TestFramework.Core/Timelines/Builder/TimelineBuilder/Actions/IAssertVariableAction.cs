using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IAssertVariableAction
{
    public ITimelineBuilderModifier AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate);
}