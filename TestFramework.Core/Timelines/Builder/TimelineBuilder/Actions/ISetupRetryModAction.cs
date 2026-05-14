using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifiers for configuring step retries.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetupRetryModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Configures retry count and delay using variable references.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay);

    /// <summary>
    /// Configures retry count using a variable reference and delay using a constant strategy.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay);
}