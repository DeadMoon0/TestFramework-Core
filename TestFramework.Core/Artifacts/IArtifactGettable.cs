namespace TestFramework.Core.Artifacts;

/// <summary>
/// Defines a typed contract for retrieving an artifact describer.
/// </summary>
public interface IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    /// <summary>
    /// Returns the typed artifact describer.
    /// </summary>
    public TArtifactDescriber GetArtifactDescriber();
}

/// <summary>
/// Defines a non-generic contract for retrieving an artifact describer.
/// </summary>
public interface IArtifactGettableGeneric
{
    /// <summary>
    /// Returns the artifact describer through the non-generic contract.
    /// </summary>
    public ArtifactDescriberGeneric GetArtifactDescriberGeneric();
}