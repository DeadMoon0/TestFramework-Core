using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Runner;

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
    internal void AddVersion(TArtifactData artifact, RunWriteTicket ticket) => base.AddVersionGeneric(artifact, ticket);

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
public class ArtifactInstanceGeneric
{
    // Swapped whole rather than mutated: two steps versioning one artifact at once would otherwise race a
    // plain list and lose a version, and a reader would see a list mid-change. Every read takes one snapshot,
    // so an index and a count always describe the same versions.
    private ImmutableList<ArtifactDataGeneric> _dataVersions = ImmutableList<ArtifactDataGeneric>.Empty;

    /// <summary>
    /// Gets an artifact data version by index.
    /// </summary>
    public ArtifactDataGeneric this[int index]
    {
        get { return Versions[index]; }
    }

    /// <summary>
    /// Gets an artifact data version by version identifier.
    /// </summary>
    public ArtifactDataGeneric this[ArtifactVersionIdentifier identifier]
    {
        get
        {
            ImmutableList<ArtifactDataGeneric> versions = Versions;
            return versions.FirstOrDefault(x => x.Identifier == identifier)
                ?? throw new ArtifactVersionNotFoundException(Identifier, identifier, versions.Select(x => x.Identifier).ToArray());
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
    public int VersionCount { get => Versions.Count; }

    private ImmutableList<ArtifactDataGeneric> Versions => Volatile.Read(ref _dataVersions);

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

    private volatile ArtifactState _state = ArtifactState.NotSetup;

    // The store that holds this instance, once one does. Every view of one run's store shares the same
    // identity, so an attempt's view and the run's own agree; another run's store does not.
    private object? _heldBy;

    /// <summary>
    /// Records that <paramref name="store"/> holds this instance.
    /// </summary>
    /// <param name="store">The holding store's identity.</param>
    /// <returns>False when a different store already holds it.</returns>
    internal bool TryHoldIn(object store)
    {
        object? holder = Interlocked.CompareExchange(ref _heldBy, store, null);
        return holder is null || ReferenceEquals(holder, store);
    }

    /// <summary>Whether a store other than <paramref name="store"/> already holds this instance.</summary>
    /// <param name="store">The asking store's identity.</param>
    internal bool IsHeldElsewhere(object store)
        => Volatile.Read(ref _heldBy) is { } holder && !ReferenceEquals(holder, store);

    /// <summary>
    /// Gets the current lifecycle state of the artifact instance.
    /// </summary>
    /// <remarks>
    /// Read-only from everywhere. It moves through <c>ArtifactStore.MarkState</c>, because setting it is a
    /// write to the run: the run's gate admits it like every other write, and the change reaches
    /// whatever is watching the run instead of only this object.
    /// </remarks>
    public ArtifactState State => _state;

    /// <summary>
    /// Moves this artifact to a new lifecycle state.
    /// </summary>
    /// <param name="state">The state it reached.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void SetState(ArtifactState state, RunWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

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
        if (firstVersionData is not null) _dataVersions = _dataVersions.Add(firstVersionData);
    }

    /// <summary>
    /// Adds a new untyped version to the artifact instance.
    /// </summary>
    /// <remarks>
    /// Ticketed for the reason <see cref="SetState"/> is: <c>ArtifactStore.CaptureVersion</c> is the one
    /// way an artifact gains a version, so the gate's admission and the publication happen every time.
    /// </remarks>
    /// <param name="data">The new version's data.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void AddVersionGeneric(ArtifactDataGeneric data, RunWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ImmutableInterlocked.Update(ref _dataVersions, static (versions, added) => versions.Add(added), data);
    }

    private ArtifactDataGeneric TryGetBoundaryVersion(bool first)
    {
        ImmutableList<ArtifactDataGeneric> versions = Versions;
        if (versions.Count > 0)
            return first ? versions[0] : versions[versions.Count - 1];

        return State switch
        {
            ArtifactState.NotFound => throw new ArtifactDoesNotExistException(Identifier),
            ArtifactState.Cleaned => throw new ArtifactDoesNotExistException(Identifier),
            _ => throw new ArtifactDoesNotYetExistException(Identifier)
        };
    }
}