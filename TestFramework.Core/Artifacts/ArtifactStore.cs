using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Stores artifact instances for a timeline run and reports updates to logging and debugging surfaces.
/// </summary>
public class ArtifactStore : IFreezable
{
    private readonly object syncRoot = new();

    /// <summary>
    /// Gets a value indicating whether the artifact store has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the artifact store.
    /// </summary>
    public void Freeze() { lock (syncRoot) { IsFrozen = true; _artifacts.Freeze(); } }

    private readonly FreezableDictionary<ArtifactIdentifier, ArtifactInstanceGeneric> _artifacts = [];
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    internal ArtifactStore(ScopedLogger logger, DebuggingRunSession debuggingSession)
    {
        this.logger = logger;
        this.debuggingSession = debuggingSession;
    }

    /// <summary>
    /// Adds or replaces an artifact instance in the store.
    /// </summary>
    public void AddArtifact(ArtifactInstanceGeneric instance)
    {
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
    internal void PublishArtifactChanged(ArtifactInstanceGeneric instance)
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
                DisplayText = description.Summary,
                Description = description,
                SchemaKey = instance.Artifact.DebugValueSchemaKey,
                Version = currentData?.Identifier.ToString(),

                // Stated rather than left for a consumer to dig out of the Core payload below, which
                // is where these two facts lived and where nothing could rely on finding them.
                Lifecycle = new DebugValueLifecycle
                {
                    State = instance.State.ToString(),
                    Versions = [.. VersionsOf(instance)],
                    CurrentVersion = currentData?.Identifier.ToString()
                },
                Core = new JObject
                {
                    ["key"] = instance.Identifier.Identifier,
                    ["artifactType"] = instance.Artifact.ToString(),
                    ["state"] = instance.State.ToString(),
                    ["versionCount"] = instance.VersionCount,
                    ["versionIndex"] = instance.VersionCount - 1,

                    // The identifiers of every captured version, oldest first, so a consumer can
                    // draw the artifact's history from one update instead of stitching together
                    // the updates it happened to be connected for.
                    ["versions"] = DescribeVersions(instance),
                    ["reference"] = ToToken(instance.Reference),
                    ["data"] = ToToken(currentData)
                },
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