using System.ComponentModel;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier that protects the artifacts of the current step from cleanup.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMarkReadonlyModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Marks every artifact the current step produces readonly, so teardown leaves the underlying
    /// resource in place.
    /// </summary>
    /// <remarks>
    /// Deleting is always the default - this is the opt-out, and it is the test author's to make.
    /// It overrides whatever the artifact reference reports about its own deconstructability, so no
    /// finder or reference type can take the resource down anyway.
    /// </remarks>
    /// <returns>The builder modifier, so the fluent chain continues.</returns>
    ITimelineBuilderModifier<TStepResultContext> MarkReadonly();
}
