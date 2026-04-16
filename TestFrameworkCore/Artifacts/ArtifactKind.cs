namespace TestFrameworkCore.Artifacts;

public abstract class ArtifactKind</*TArtifactKind,*/ TArtifactDescriber, TArtifactData, TArtifactReference>
    //where TArtifactKind : ArtifactKind<TArtifactKind, TArtifactDescriber, TArtifactData, TArtifactReference>, IStaticArtifactKind<TArtifactKind> // Dont go over your self
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>;

public abstract class ArtifactKindGeneric;