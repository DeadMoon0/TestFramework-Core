using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Stores artifact instances for a timeline run and reports updates to logging and debugging surfaces.
/// </summary>
public class ArtifactStore : IFreezable
{
    private readonly object syncRoot;

    /// <summary>
    /// Gets a value indicating whether the artifact store has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen => _artifacts.IsFrozen;

    /// <summary>
    /// Freezes the artifact store against further mutation.
    /// </summary>
    /// <remarks>
    /// Reached through <see cref="IFreezable"/> rather than offered on the store itself: the run freezes
    /// its own artifacts when it ends, and anything else doing it mid-run would turn every later capture
    /// into a failure.
    /// </remarks>
    void IFreezable.Freeze() { lock (syncRoot) { _artifacts.Freeze(); } }

    internal void FreezeForRunEnd() { lock (syncRoot) { _artifacts.Freeze(); } }

    private readonly FreezableDictionary<ArtifactIdentifier, ArtifactInstanceGeneric> _artifacts;
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    /// <summary>
    /// Whether writes through this view of the store still count.
    /// </summary>
    /// <remarks>
    /// Unrestricted on the run's own store: an artifact seeded before the run starts belongs to no step.
    /// </remarks>
    private readonly StepWriteLicence licence;

    /// <summary>
    /// A view of this store that writes on behalf of one attempt at one step.
    /// </summary>
    /// <remarks>
    /// The artifact half of the quarantine the variables already have. Without it, a step abandoned at
    /// its deadline could still register an artifact - or capture a version of one - into the store the
    /// next test reads, and that is the more expensive half of the bug: a variable holds a value, an
    /// artifact holds a row in somebody's database and a promise to clean it up.
    /// </remarks>
    /// <param name="gate">The gate holding the current attempt.</param>
    /// <param name="attempt">The attempt this view writes for.</param>
    /// <returns>The view.</returns>
    internal ArtifactStore ForAttempt(StepAttemptGate gate, StepAttempt attempt)
        => new ArtifactStore(this, gate, attempt);

    private ArtifactStore(ArtifactStore source, StepAttemptGate gate, StepAttempt attempt)
    {
        // Shared by reference on purpose: one store, many writers, each able to say who it is.
        this.syncRoot = source.syncRoot;
        this._artifacts = source._artifacts;
        this.logger = source.logger;
        this.debuggingSession = source.debuggingSession;
        this.licence = StepWriteLicence.For(gate, attempt);
    }

    internal ArtifactStore(ScopedLogger logger, DebuggingRunSession debuggingSession)
    {
        this.syncRoot = new object();
        this._artifacts = [];
        this.logger = logger;
        this.debuggingSession = debuggingSession;
        this.licence = StepWriteLicence.Unrestricted;
    }

    /// <summary>
    /// The only ticket there is. Private, so nothing else can name it, let alone make one.
    /// </summary>
    private sealed class Ticket : ArtifactWriteTicket
    {
    }

    /// <summary>
    /// Makes an artifact and holds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one way to bring an artifact into being. An instance's constructor is internal - it has to be,
    /// or anything could mint one with a state nobody set and a reference nothing pinned - and until this
    /// existed there was no public way at all: Core built them inside its own builder, and two packages'
    /// test suites reflected over the internal constructor to get one, which broke the moment that
    /// constructor gained a parameter.
    /// </para>
    /// <para>
    /// A package needs this for a real reason, not only for tests: an environment provider decides which
    /// components a set of artifacts requires, and that means having artifacts to hand it.
    /// </para>
    /// </remarks>
    /// <typeparam name="TArtifactDescriber">The describer.</typeparam>
    /// <typeparam name="TArtifactData">The data.</typeparam>
    /// <typeparam name="TArtifactReference">The reference.</typeparam>
    /// <param name="identifier">What to call it.</param>
    /// <param name="reference">Where it lives.</param>
    /// <param name="data">Its first version, when it already has one.</param>
    /// <param name="state">The state it exists in from the start.</param>
    /// <param name="isReadonly">Whether cleanup must leave the underlying resource alone.</param>
    /// <returns>The artifact, already held by this store.</returns>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> Add<TArtifactDescriber, TArtifactData, TArtifactReference>(
        ArtifactIdentifier identifier,
        TArtifactReference reference,
        TArtifactData? data = null,
        ArtifactState state = ArtifactState.NotSetup,
        bool isReadonly = false)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        ArgumentNullException.ThrowIfNull(reference);

        ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> instance = new(
            reference.GetArtifactDescriber(),
            identifier,
            reference,
            data,
            state,
            isReadonly);

        this.AddArtifact(instance);

        return instance;
    }

    /// <summary>
    /// Adds or replaces an artifact instance in the store.
    /// </summary>
    /// <param name="instance">The artifact.</param>
    public void AddArtifact(ArtifactInstanceGeneric instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        this.Write(instance.Identifier, ticket => this.Hold(instance, ticket), instance);
    }

    /// <summary>
    /// Adds a version to an artifact this store holds.
    /// </summary>
    /// <param name="instance">The artifact.</param>
    /// <param name="data">The new version's data.</param>
    internal void CaptureVersion(ArtifactInstanceGeneric instance, ArtifactDataGeneric data)
        => this.Write(instance.Identifier, ticket => instance.AddVersionGeneric(data, ticket), instance);

    /// <summary>
    /// Moves an artifact to a new lifecycle state.
    /// </summary>
    /// <param name="instance">The artifact.</param>
    /// <param name="state">The state it reached.</param>
    internal void MarkState(ArtifactInstanceGeneric instance, ArtifactState state)
        => this.Write(instance.Identifier, ticket => instance.SetState(state, ticket), instance);

    /// <summary>
    /// Pins the reference of an artifact this store holds.
    /// </summary>
    /// <remarks>
    /// Pinning resolves the reference's variables and keeps the answer, so a pin from an attempt the run
    /// has stopped waiting for would aim this artifact - and the cleanup that deletes it - at whatever
    /// that attempt's stale values pointed to.
    /// </remarks>
    /// <param name="instance">The artifact.</param>
    /// <param name="context">What the step doing this was given.</param>
    internal void PinReference(ArtifactInstanceGeneric instance, RunContext context)
        => this.Write(instance.Identifier, ticket => instance.Reference.Pin(context, ticket), instance);

    /// <summary>
    /// Pins a reference for an artifact that is about to be added.
    /// </summary>
    /// <remarks>
    /// Discovery and registration pin before there is an instance to hold. The check is the same and is
    /// keyed on the identifier the artifact is about to take, so an abandoned attempt is refused here
    /// rather than three lines later when it tries to add what it pinned.
    /// </remarks>
    /// <param name="identifier">The identifier the artifact will have.</param>
    /// <param name="reference">The reference to pin.</param>
    /// <param name="context">What the step doing this was given.</param>
    internal void PinNewReference(ArtifactIdentifier identifier, ArtifactReferenceGeneric reference, RunContext context)
    {
        ArgumentNullException.ThrowIfNull(reference);

        this.Write(identifier, ticket => reference.Pin(context, ticket), published: null);
    }

    /// <summary>
    /// The one place a change to the run's artifacts happens.
    /// </summary>
    /// <remarks>
    /// One licence check, one ticket, one publication. Every write is expressed as <em>what</em> changes;
    /// none of them decides <em>whether</em> it may, which is why a new kind of write cannot arrive
    /// without the check.
    /// </remarks>
    /// <param name="target">What is being written, for the warning a refused write produces.</param>
    /// <param name="change">The change to make.</param>
    /// <param name="published">The artifact to publish afterwards, or null when there is nothing to show yet.</param>
    private void Write(ArtifactIdentifier target, Action<ArtifactWriteTicket> change, ArtifactInstanceGeneric? published)
    {
        // An attempt the runner has stopped waiting for is still running, and it must not be able to
        // reach the stores a later test reads.
        if (!licence.Allows(logger, target.Identifier))
            return;

        // Legal here and nowhere else: a nested type's private constructor is reachable from the type
        // that encloses it, and from nothing further out.
        change(new Ticket());

        // Building the debug state serialises the reference and the data. Skip it when nothing reads it.
        if (published is null || !debuggingSession.IsCapturing)
            return;

        // Every change is published, not just the ones that arrive through AddArtifact. A version landing
        // on an instance the store already holds used to be invisible to a debugger, which made watching
        // a value evolve across a run - the whole point of capturing versions - impossible to see.
        debuggingSession.PublishArtifactUpdate(published.Identifier, WithBody(GetDebuggingStateFromInstance(published), published));
    }

    private void Hold(ArtifactInstanceGeneric instance, ArtifactWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        lock (syncRoot)
        {
            _artifacts[instance.Identifier] = instance;
        }
    }

    /// <summary>
    /// Writes the artifact's data out and points the description at it, when it did not fit.
    /// </summary>
    /// <remarks>
    /// Keyed by identifier and versioned by content, so an artifact captured three times leaves three
    /// files — which is the whole point of capturing versions, and was previously visible only as
    /// three truncated previews.
    /// </remarks>
    private DebugValue WithBody(DebugValue state, ArtifactInstanceGeneric instance)
    {
        if (state.Envelope.Description.Preview?.IsTruncated != true || instance.VersionCount == 0)
            return state;

        DebugValueContent? content = DebugValueDescriber.FullContentOf(instance.Last);

        if (content is null)
            return state;

        DebugValueDescription described = state.Envelope.Description with
        {
            Body = debuggingSession.ValueFiles.Write(instance.Identifier.Identifier, content)
        };

        return state with { Envelope = state.Envelope with { Description = described } };
    }

    internal static DebugValue GetDebuggingStateFromInstance(ArtifactInstanceGeneric instance)
    {
        ArtifactDataGeneric? currentData = instance.VersionCount != 0 ? instance[instance.VersionCount - 1] : null;

        // Asked of the artifact kind rather than assembled here, so an artifact that knows what it
        // is — a row, a blob, a file — can say so instead of being described by its serialised
        // reference.
        DebugValueDescription description = instance.Artifact.Describe(instance);

        return new DebugValue
        {
            Key = instance.Identifier,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Artifact,
                TypeName = instance.Artifact.GetType().FullName ?? instance.Artifact.ToString(),
                Description = description,
                SchemaKey = instance.Artifact.DebugValueSchemaKey,

                // The state and the whole version history, every time, so a consumer that attached late - or
                // replayed a journal missing the earlier events - still sees v1 -> v2 -> v3 rather than only
                // what it happened to witness. These were once buried in a loose JSON payload beside this
                // one, where nothing could rely on finding them.
                Lifecycle = new DebugValueLifecycle
                {
                    State = instance.State.ToString(),
                    Versions = [.. VersionsOf(instance)],
                    CurrentVersion = currentData?.Identifier.ToString()
                },

                // What this kind of artifact wants to say about itself, and the only free-form payload left.
                // The reference and the current data are described as facts by the artifact's own describer,
                // which is the thing that knows which of them are worth stating.
                Custom = instance.Artifact.CreateDebugValueCustomPayload(instance)
            }
        };
    }

    private static IEnumerable<string> VersionsOf(ArtifactInstanceGeneric instance)
    {
        for (int index = 0; index < instance.VersionCount; index++)
            yield return instance[index].Identifier.ToString();
    }

    /// <summary>
    /// Gets an artifact instance by identifier.
    /// </summary>
    public ArtifactInstanceGeneric GetArtifact(ArtifactIdentifier identifier)
    {
        lock (syncRoot)
        {
            if (_artifacts.TryGetValue(identifier, out ArtifactInstanceGeneric? instance))
                return instance;

            throw new ArtifactNotFoundException(identifier.Identifier, _artifacts.Keys.Select(k => k.Identifier).OrderBy(x => x).ToArray());
        }
    }

    /// <summary>
    /// Gets a typed artifact instance by identifier using an explicit kind token.
    /// </summary>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactKind<TArtifactDescriber, TArtifactData, TArtifactReference> kind, ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)GetArtifact(identifier);
    }

    /// <summary>
    /// Gets a typed artifact instance by identifier.
    /// </summary>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)GetArtifact(identifier);
    }

    /// <summary>
    /// Returns all artifact instances currently stored for the run.
    /// </summary>
    public IEnumerable<ArtifactInstanceGeneric> GetAll()
    {
        lock (syncRoot)
        {
            return _artifacts.Values.ToArray();
        }
    }

}