using System.ComponentModel;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for assigning a label to the current step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ILabelModAction<TStepResultContext> : IImplementedByCore where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Assigns a consumer-visible label to the current typed step.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> Label(string label);
}
