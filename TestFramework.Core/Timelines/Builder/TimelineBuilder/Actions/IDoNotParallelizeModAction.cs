using System.ComponentModel;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for marking a step as non-parallelizable.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDoNotParallelizeModAction<TStepResultContext> : IImplementedByCore where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Marks the current typed step so it will not run concurrently with other steps.
    /// </summary>
    ITimelineBuilderModifier<TStepResultContext> DoNotParallelize();
}