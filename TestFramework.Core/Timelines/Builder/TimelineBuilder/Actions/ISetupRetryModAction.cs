using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifiers for configuring step retries.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetupRetryModAction
{
    /// <summary>
    /// Configures retry count and delay using variable references.
    /// </summary>
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay);

    /// <summary>
    /// Configures retry count using a variable reference and delay using a constant strategy.
    /// </summary>
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay);
}