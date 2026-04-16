using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IRegisterArtifactAction
{
    public ITimelineBuilderModifier RegisterArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>;
}