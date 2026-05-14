using System.ComponentModel;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for assigning a label to the current step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INameModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Assigns a consumer-visible label to the current typed step.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> Name(string label);
}
