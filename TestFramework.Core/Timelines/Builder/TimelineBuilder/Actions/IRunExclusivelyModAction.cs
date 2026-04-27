using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for marking a step as exclusive.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRunExclusivelyModAction
{
    /// <summary>
    /// Marks the current step so it will not run concurrently with other steps.
    /// </summary>
    public ITimelineBuilderModifier RunExclusively();
}
