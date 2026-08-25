using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;

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
    /// Adds or replaces an artifact instance in the store.
    /// </summary>
    public void AddArtifact(ArtifactInstanceGeneric instance)
    {
        // An attempt the runner has stopped waiting for is still running, and it must not be able to
        // reach the stores a later test reads.
        if (!licence.Allows(logger, instance.Identifier.Identifier))
            return;

        lock (syncRoot)
        {
            _artifacts[instance.Identifier] = instance;
        }

        // Building the debug state serializes the reference and the data. Skip it when nothing reads it.
        if (!debuggingSession.IsCapturing)
            return;

        debuggingSession.PublishArtifactUpdate(instance.Identifier, WithBody(GetDebuggingStateFromInstance(instance), instance));
    }

    /// <summary>
    /// Re-publishes an artifact whose contents changed in place, rather than through
    /// <see cref="AddArtifact"/>.
    /// </summary>
    /// <remarks>
    /// Capturing a version and changing lifecycle state both mutate the instance the store already
    /// holds, so neither passes through <see cref="AddArtifact"/>. Without this, a debugger only
    /// ever saw an artifact's first version and its initial state: the whole point of
    /// <c>CaptureArtifactVersion</c> — watching a value evolve across a run — was invisible to the
    /// one consumer built to show it.
    /// </remarks>
    /// <summary>
    /// Adds a version to an artifact this store already holds.
    /// </summary>
    /// <remarks>
    /// An artifact changes in three ways - it is added, it gains a version, its lifecycle state moves -
    /// and all three have to pass the same licence check, so all three are asked of the store. That is
    /// also why the mutators on the instance are internal: a step holding an instance could otherwise
    /// change the run's artifacts without the store ever hearing about it, which is both a way around
    /// the quarantine and the reason a debugger used to miss the change.
    /// </remarks>
    /// <param name="instance">The artifact.</param>
    /// <param name="data">The new version's data.</param>
    internal void CaptureVersion(ArtifactInstanceGeneric instance, ArtifactDataGeneric data)
    {
        if (!licence.Allows(logger, instance.Identifier.Identifier))
            return;

        instance.AddVersionGeneric(data);
        PublishArtifactChanged(instance);
    }

    /// <summary>
    /// Moves an artifact to a new lifecycle state.
    /// </summary>
    /// <param name="instance">The artifact.</param>
    /// <param name="state">The state it reached.</param>
    internal void MarkState(ArtifactInstanceGeneric instance, ArtifactState state)
    {
        if (!licence.Allows(logger, instance.Identifier.Identifier))
            return;

        instance.State = state;
        PublishArtifactChanged(instance);
    }

    private void PublishArtifactChanged(ArtifactInstanceGeneric instance)
    {
        if (!debuggingSession.IsCapturing)
            return;

        debuggingSession.PublishArtifactUpdate(instance.Identifier, WithBody(GetDebuggingStateFromInstance(instance), instance));
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

    private static JArray DescribeVersions(ArtifactInstanceGeneric instance)
    {
        JArray versions = [];

        foreach (string version in VersionsOf(instance))
            versions.Add(version);

        return versions;
    }

    private static IEnumerable<string> VersionsOf(ArtifactInstanceGeneric instance)
    {
        for (int index = 0; index < instance.VersionCount; index++)
            yield return instance[index].Identifier.ToString();
    }

    private static JToken ToToken(object? value)
    {
        if (value is null)
            return JValue.CreateNull();

        try
        {
            return JToken.FromObject(value, JsonSerializer.CreateDefault());
        }
        catch (JsonException)
        {
            // A debug payload that cannot be serialized must not take the run down with it.
            return new JValue($"<unserializable {value.GetType().FullName}>");
        }
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