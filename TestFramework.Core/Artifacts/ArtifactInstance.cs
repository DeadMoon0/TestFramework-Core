using System;
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

    internal ArtifactInstance(TArtifactDescriber artifact, ArtifactIdentifier identifier, TArtifactReference reference, TArtifactData? firstVersionData, ArtifactState state = ArtifactState.NotSetup, bool isReadonly = false)
        : base(artifact, identifier, reference, firstVersionData, state, isReadonly) { }

    /// <summary>
    /// Adds a new typed version to the artifact instance.
    /// </summary>
    /// <remarks>
    /// Asked of the store rather than of the instance - see <c>ArtifactStore.CaptureVersion</c> - and the
    /// ticket is what makes that the only route: nothing outside the store can make one.
    /// </remarks>
    /// <param name="artifact">The new version's data.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void AddVersion(TArtifactData artifact, ArtifactWriteTicket ticket) => base.AddVersionGeneric(artifact, ticket);

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
    /// artifact into a failure. The reference and the describer are settled with it - a frozen artifact
    /// whose reference could still be re-pinned would be frozen in name only.
    /// </remarks>
    void IFreezable.Freeze()
    {
        IsFrozen = true;
        _dataVersions.Freeze();
        ((IFreezable)Reference).Freeze();
        ((IFreezable)Artifact).Freeze();
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
    /// Read-only from everywhere. It moves through <c>ArtifactStore.MarkState</c>, because setting it is a
    /// write to the run: it passes the same licence check as every other write, and the change reaches
    /// whatever is watching the run instead of only this object.
    /// </remarks>
    public ArtifactState State => _state;

    /// <summary>
    /// Moves this artifact to a new lifecycle state.
    /// </summary>
    /// <param name="state">The state it reached.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void SetState(ArtifactState state, ArtifactWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ((IFreezable)this).EnsureNotFrozen();

        _state = state;
    }

    /// <summary>
    /// Gets a value indicating whether the timeline marked this artifact readonly, so cleanup must
    /// leave the underlying resource in place.
    /// </summary>
    /// <remarks>
    /// This is the test author's decision, taken at the <c>RegisterArtifact</c> / <c>FindArtifact</c>
    /// call site through <c>MarkReadonly()</c>. It is deliberately separate from
    /// <see cref="ArtifactReferenceGeneric.CanDeconstruct"/>: the reference answers whether the
    /// resource <em>can</em> be deconstructed, this answers whether it <em>may</em> be. Decided when the
    /// artifact is made and never afterwards, so no reference type, finder, or package can overrule the
    /// choice - not even by being wrong once.
    /// </remarks>
    public bool IsReadonly { get; }

    /// <summary>
    /// Creates an artifact instance in the state it was born in.
    /// </summary>
    /// <remarks>
    /// The state is a constructor argument rather than something assigned afterwards, so that
    /// <see cref="State"/>'s setter has exactly one caller - <c>ArtifactStore.MarkState</c> - and
    /// "an artifact's state only ever moves through the store" is true rather than nearly true. A
    /// discovered artifact is born <c>Setup</c> or <c>NotFound</c>; nothing moved it there.
    /// </remarks>
    /// <param name="artifact">The describer.</param>
    /// <param name="identifier">The identifier.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="firstVersionData">The first version's data, when it already has one.</param>
    /// <param name="state">The state it exists in from the start.</param>
    /// <param name="isReadonly">Whether the timeline marked it readonly.</param>
    internal ArtifactInstanceGeneric(
        ArtifactDescriberGeneric artifact,
        ArtifactIdentifier identifier,
        ArtifactReferenceGeneric reference,
        ArtifactDataGeneric? firstVersionData,
        ArtifactState state = ArtifactState.NotSetup,
        bool isReadonly = false)
    {
        Artifact = artifact;
        Identifier = identifier;
        Reference = reference;
        IsReadonly = isReadonly;
        _state = state;

        // Construction, not a write: there is no store yet, and nothing to tell about it.
        if (firstVersionData is not null) _dataVersions.Add(firstVersionData);
    }

    /// <summary>
    /// Adds a new untyped version to the artifact instance.
    /// </summary>
    /// <remarks>
    /// Ticketed for the reason <see cref="SetState"/> is: <c>ArtifactStore.CaptureVersion</c> is the one
    /// way an artifact gains a version, so the licence check and the publication happen every time.
    /// </remarks>
    /// <param name="data">The new version's data.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void AddVersionGeneric(ArtifactDataGeneric data, ArtifactWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
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