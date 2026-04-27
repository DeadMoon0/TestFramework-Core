using TestFramework.Core.Artifacts;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for registering an artifact reference in the timeline.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRegisterArtifactAction
{
    /// <summary>
    /// Registers an artifact reference under the specified identifier.
    /// </summary>
    public ITimelineBuilderModifier RegisterArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>;
}