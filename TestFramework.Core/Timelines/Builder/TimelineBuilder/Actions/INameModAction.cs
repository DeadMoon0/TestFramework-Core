using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for assigning a label to the current step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INameModAction
{
    /// <summary>
    /// Assigns a consumer-visible label to the current step.
    /// </summary>
    public ITimelineBuilderModifier Name(string label);
}
