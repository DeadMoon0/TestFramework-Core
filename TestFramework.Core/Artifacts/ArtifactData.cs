namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents typed artifact data that can report its associated artifact describer.
/// </summary>
public abstract class ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference> : ArtifactDataGeneric, IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    /// <summary>
    /// Returns the typed artifact describer for this artifact data.
    /// </summary>
    public virtual TArtifactDescriber GetArtifactDescriber()
    {
        return new TArtifactDescriber();
    }

    /// <summary>
    /// Returns the artifact describer through the non-generic base contract.
    /// </summary>
    public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => GetArtifactDescriber();
}

/// <summary>
/// Represents non-generic artifact data.
/// </summary>
public abstract class ArtifactDataGeneric : IArtifactGettableGeneric
{
    /// <summary>
    /// Gets the artifact version identifier carried by this data payload.
    /// </summary>
    public ArtifactVersionIdentifier Identifier { get; init; } = ArtifactVersionIdentifier.Default;

    /// <summary>
    /// Returns the artifact describer through the non-generic base contract.
    /// </summary>
    public abstract ArtifactDescriberGeneric GetArtifactDescriberGeneric();

    /// <summary>
    /// Returns a human-readable representation of the artifact data.
    /// </summary>
    public abstract override string ToString();
}