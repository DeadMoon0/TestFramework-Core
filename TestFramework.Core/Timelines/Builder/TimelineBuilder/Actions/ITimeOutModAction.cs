using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ITimeOutModAction
{
    public ITimelineBuilderModifier WithTimeOut(VariableReference<TimeSpan> timeout);
}