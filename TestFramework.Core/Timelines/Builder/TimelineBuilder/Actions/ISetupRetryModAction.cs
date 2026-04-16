using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetupRetryModAction
{
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay);
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay);
}