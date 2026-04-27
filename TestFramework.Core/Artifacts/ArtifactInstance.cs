using System.Linq;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents the lifecycle state of an artifact instance.
/// </summary>
public enum ArtifactState
{
    /// <summary>
    /// The artifact has not been set up yet.
    /// </summary>
    NotSetup,
    /// <summary>
    /// The artifact has been set up and is available.
    /// </summary>
    Setup,
    /// <summary>
    /// The artifact has been cleaned up.
    /// </summary>
    Cleaned,
    /// <summary>
    /// The artifact could not be found.
    /// </summary>
    NotFound
}

/// <summary>
/// Represents a typed artifact instance and its versioned data payloads.
/// </summary>
public class ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactInstanceGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    /// <summary>
    /// Gets the typed artifact describer.
    /// </summary>
    public new TArtifactDescriber Artifact { get => (TArtifactDescriber)base.Artifact; }

    /// <summary>
    /// Gets the typed artifact reference.
    /// </summary>
    public new TArtifactReference Reference { get => (TArtifactReference)base.Reference; }

    /// <summary>
    /// Gets a typed artifact data version by index.
    /// </summary>
    public new TArtifactData this[int index]
    {
        get { return (TArtifactData)base[index]; }
    }

    /// <summary>
    /// Gets a typed artifact data version by version identifier.
    /// </summary>
    public new TArtifactData this[ArtifactVersionIdentifier identifier]
    {
        get { return (TArtifactData)base[identifier]; }
    }

    /// <summary>
    /// Gets the first typed artifact data version.
    /// </summary>
    public new TArtifactData First { get => (TArtifactData)base.First; }

    /// <summary>
    /// Gets the latest typed artifact data version.
    /// </summary>
    public new TArtifactData Last { get => (TArtifactData)base.Last; }

    internal ArtifactInstance(TArtifactDescriber artifact, ArtifactIdentifier identifier, TArtifactReference reference, TArtifactData? firstVersionData) : base(artifact, identifier, reference, firstVersionData) { }

    /// <summary>
    /// Adds a new typed version to the artifact instance.
    /// </summary>
    public void AddVersion(TArtifactData artifact) => base.AddVersionGeneric(artifact);

    /// <summary>
    /// Returns a human-readable description of the artifact instance.
    /// </summary>
    public override string ToString()
    {
        return $"Ref: {Reference}; Describer: {Artifact}; Data: {(VersionCount != 0 ? this[VersionCount - 1] : null)}";
    }
}

/// <summary>
/// Represents an untyped artifact instance and its versioned data payloads.
/// </summary>
public class ArtifactInstanceGeneric : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the artifact instance has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the artifact instance.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
    }

    private readonly FreezableCollection<ArtifactDataGeneric> _dataVersions = [];

    /// <summary>
    /// Gets an artifact data version by index.
    /// </summary>
    public ArtifactDataGeneric this[int index]
    {
        get { return _dataVersions.ElementAt(index); }
    }

    /// <summary>
    /// Gets an artifact data version by version identifier.
    /// </summary>
    public ArtifactDataGeneric this[ArtifactVersionIdentifier identifier]
    {
        get { return _dataVersions.FirstOrDefault(x => x.Identifier == identifier) ?? throw new System.InvalidOperationException("No Version has the Identifier: '" + identifier + "'."); }
    }

    /// <summary>
    /// Gets the first artifact data version.
    /// </summary>
    public ArtifactDataGeneric First { get => _dataVersions.First(); }

    /// <summary>
    /// Gets the latest artifact data version.
    /// </summary>
    public ArtifactDataGeneric Last { get => _dataVersions.Last(); }

    /// <summary>
    /// Gets the number of data versions stored for the artifact.
    /// </summary>
    public int VersionCount { get => _dataVersions.Count; }

    /// <summary>
    /// Gets the artifact identifier.
    /// </summary>
    public ArtifactIdentifier Identifier { get; }

    /// <summary>
    /// Gets the artifact describer.
    /// </summary>
    public ArtifactDescriberGeneric Artifact { get; }

    /// <summary>
    /// Gets the artifact reference.
    /// </summary>
    public ArtifactReferenceGeneric Reference { get; }

    private ArtifactState _state = ArtifactState.NotSetup;

    /// <summary>
    /// Gets or sets the current lifecycle state of the artifact instance.
    /// </summary>
    public ArtifactState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }

    internal ArtifactInstanceGeneric(ArtifactDescriberGeneric artifact, ArtifactIdentifier identifier, ArtifactReferenceGeneric reference, ArtifactDataGeneric? firstVersionData)
    {
        Artifact = artifact;
        Identifier = identifier;
        Reference = reference;
        if (firstVersionData is not null) AddVersionGeneric(firstVersionData);
    }

    /// <summary>
    /// Adds a new untyped version to the artifact instance.
    /// </summary>
    public void AddVersionGeneric(ArtifactDataGeneric data)
    {
        _dataVersions.Add(data);
    }
}