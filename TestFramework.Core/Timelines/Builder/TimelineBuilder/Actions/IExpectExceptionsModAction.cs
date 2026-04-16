using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IExpectExceptionsModAction
{
    public ITimelineBuilderModifier ExpectExceptions(params Type[] exceptionTypes);
}