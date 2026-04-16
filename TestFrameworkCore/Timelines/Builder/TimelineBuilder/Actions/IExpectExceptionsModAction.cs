using System;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface IExpectExceptionsModAction
{
    public ITimelineBuilderModifier ExpectExceptions(params Type[] exceptionTypes);
}