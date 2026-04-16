namespace TestFramework.Core.Artifacts;

public abstract class ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference> : ArtifactDataGeneric, IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public virtual TArtifactDescriber GetArtifactDescriber()
    {
        return new TArtifactDescriber();
    }

    public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => GetArtifactDescriber();
}

public abstract class ArtifactDataGeneric : IArtifactGettableGeneric
{
    public ArtifactVersionIdentifier Identifier { get; init; } = ArtifactVersionIdentifier.Default;
    public abstract ArtifactDescriberGeneric GetArtifactDescriberGeneric();
    public abstract override string ToString();
}