using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetupRetryModAction
{
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay);
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay);
}