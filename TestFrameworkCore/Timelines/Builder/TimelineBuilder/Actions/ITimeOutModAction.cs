using System;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ITimeOutModAction
{
    public ITimelineBuilderModifier WithTimeOut(VariableReference<TimeSpan> timeout);
}