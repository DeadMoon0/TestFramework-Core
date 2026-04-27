using TestFramework.Core.Artifacts;

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
    public ITimelineBuilderModifier RemoveArtifact(ArtifactIdentifier identifier);
}