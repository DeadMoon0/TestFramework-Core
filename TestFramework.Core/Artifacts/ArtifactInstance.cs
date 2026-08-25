using System.Linq;
using TestFramework.Core.Exceptions;

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
    /// <remarks>
    /// Asked of the store rather than of the instance - see <c>ArtifactStore.CaptureVersion</c> - so that
    /// a version cannot land from an attempt the run has stopped waiting for, and so that nothing watching
    /// the run can miss it.
    /// </remarks>
    internal void AddVersion(TArtifactData artifact) => base.AddVersionGeneric(artifact);

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
    /// Freezes the artifact instance against further mutation.
    /// </summary>
    /// <remarks>
    /// Reached through <see cref="IFreezable"/> rather than offered on the instance: the run freezes its
    /// artifacts when it ends, and anything else doing it mid-run would turn every later capture of that
    /// artifact into a failure.
    /// </remarks>
    void IFreezable.Freeze()
    {
        IsFrozen = true;
        _dataVersions.Freeze();
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
        get
        {
            return _dataVersions.FirstOrDefault(x => x.Identifier == identifier)
                ?? throw new ArtifactVersionNotFoundException(Identifier, identifier, _dataVersions.Select(x => x.Identifier).ToArray());
        }
    }

    /// <summary>
    /// Gets the first artifact data version.
    /// </summary>
    public ArtifactDataGeneric First { get => TryGetBoundaryVersion(first: true); }

    /// <summary>
    /// Gets the latest artifact data version.
    /// </summary>
    public ArtifactDataGeneric Last { get => TryGetBoundaryVersion(first: false); }

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
    /// Gets the current lifecycle state of the artifact instance.
    /// </summary>
    /// <remarks>
    /// Moved through <c>ArtifactStore.MarkState</c> rather than assigned here: setting it is a write to the
    /// run, so it passes the same licence check as every other write, and the change reaches whatever is
    /// watching the run instead of only the object.
    /// </remarks>
    public ArtifactState State { get => _state; internal set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }

    private bool _isReadonly;

    /// <summary>
    /// Gets a value indicating whether the timeline marked this artifact readonly, so cleanup must
    /// leave the underlying resource in place.
    /// </summary>
    /// <remarks>
    /// This is the test author's decision, taken at the <c>RegisterArtifact</c> / <c>FindArtifact</c>
    /// call site through <c>MarkReadonly()</c>. It is deliberately separate from
    /// <see cref="ArtifactReferenceGeneric.CanDeconstruct"/>: the reference answers whether the
    /// resource <em>can</em> be deconstructed, this answers whether it <em>may</em> be. The setter is
    /// internal so no reference type, finder, or package can overrule the choice.
    /// </remarks>
    public bool IsReadonly { get => _isReadonly; internal set { ((IFreezable)this).EnsureNotFrozen(); _isReadonly = value; } }

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
    /// <remarks>
    /// Internal for the reason <see cref="State"/> is: <c>ArtifactStore.CaptureVersion</c> is the one way
    /// an artifact gains a version, so the licence check and the publication happen every time.
    /// </remarks>
    internal void AddVersionGeneric(ArtifactDataGeneric data)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _dataVersions.Add(data);
    }

    private ArtifactDataGeneric TryGetBoundaryVersion(bool first)
    {
        if (_dataVersions.Count > 0)
            return first ? _dataVersions.First() : _dataVersions.Last();

        return State switch
        {
            ArtifactState.NotFound => throw new ArtifactDoesNotExistException(Identifier),
            ArtifactState.Cleaned => throw new ArtifactDoesNotExistException(Identifier),
            _ => throw new ArtifactDoesNotYetExistException(Identifier)
        };
    }
}