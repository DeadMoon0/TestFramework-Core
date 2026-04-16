namespace TestFrameworkCore.Artifacts;

public interface IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public TArtifactDescriber GetArtifactDescriber();
}

public interface IArtifactGettableGeneric
{
    public ArtifactDescriberGeneric GetArtifactDescriberGeneric();
}