using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for capturing artifact versions.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IVersionArtifactAction
{
    /// <summary>
    /// Captures a new version for the specified artifact using an inferred version identifier.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier);

    /// <summary>
    /// Captures a new version for the specified artifact using the provided version identifier.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier);
}