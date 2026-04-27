using System.ComponentModel;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

/// <summary>
/// Adds the fluent verb for seeding artifacts into a timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAddArtifactAction
{
    /// <summary>
    /// Adds an initial artifact and data payload to the run configuration.
    /// </summary>
    public ITimelineRunBuilder AddArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference, ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference> data)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>;
}