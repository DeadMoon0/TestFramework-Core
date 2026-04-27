namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents the typed kind of an artifact contract.
/// </summary>
public abstract class ArtifactKind</*TArtifactKind,*/ TArtifactDescriber, TArtifactData, TArtifactReference>
    //where TArtifactKind : ArtifactKind<TArtifactKind, TArtifactDescriber, TArtifactData, TArtifactReference>, IStaticArtifactKind<TArtifactKind> // Dont go over your self
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>;

/// <summary>
/// Represents the non-generic base kind of an artifact contract.
/// </summary>
public abstract class ArtifactKindGeneric;