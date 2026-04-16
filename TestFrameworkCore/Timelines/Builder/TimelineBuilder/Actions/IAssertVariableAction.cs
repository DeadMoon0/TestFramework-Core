using System;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface IAssertVariableAction
{
    public ITimelineBuilderModifier AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate);
}