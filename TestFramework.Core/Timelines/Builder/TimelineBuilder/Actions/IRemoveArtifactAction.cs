using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for removing an artifact from the timeline state.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRemoveArtifactAction
{
    /// <summary>
    /// Adds a step that removes the artifact associated with the identifier.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> RemoveArtifact(ArtifactIdentifier identifier);
}