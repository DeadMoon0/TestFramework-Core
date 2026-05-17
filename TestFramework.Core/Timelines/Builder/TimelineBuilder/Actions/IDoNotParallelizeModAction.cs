using System.ComponentModel;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for marking a step as non-parallelizable.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDoNotParallelizeModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Marks the current typed step so it will not run concurrently with other steps.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> DoNotParallelize();
}