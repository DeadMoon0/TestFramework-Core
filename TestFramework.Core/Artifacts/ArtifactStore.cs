using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
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
    public void Freeze() { lock (syncRoot) { IsFrozen = true; } }

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
        ArtifactInstanceGeneric? previous;
        bool replaced;

        lock (syncRoot)
        {
            replaced = _artifacts.TryGetValue(instance.Identifier, out previous);
            _artifacts[instance.Identifier] = instance;
        }

        debuggingSession.UpdateArtifactAsync(instance.Identifier, GetDebuggingStateFromInstance(instance));
    }

    internal static Debugger.ArtifactState GetDebuggingStateFromInstance(ArtifactInstanceGeneric instance)
    {
        ArtifactDataGeneric? currentData = instance.VersionCount != 0 ? instance[instance.VersionCount - 1] : null;
        return new Debugger.ArtifactState
        {
            Key = instance.Identifier,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Artifact,
                TypeName = instance.Artifact.GetType().FullName ?? instance.Artifact.ToString(),
                DisplayText = DescribeArtifact(instance),
                SchemaKey = instance.Artifact.DebugValueSchemaKey,
                Version = currentData?.Identifier.ToString(),
                Core = new JObject
                {
                    ["key"] = instance.Identifier.Identifier,
                    ["artifactType"] = instance.Artifact.ToString(),
                    ["state"] = instance.State.ToString(),
                    ["versionCount"] = instance.VersionCount,
                    ["reference"] = ToToken(instance.Reference),
                    ["data"] = ToToken(currentData)
                },
                Custom = instance.Artifact.CreateDebugValueCustomPayload(instance)
            }
        };
    }

    private static JToken ToToken(object? value)
    {
        return value is null ? JValue.CreateNull() : JToken.FromObject(value, JsonSerializer.CreateDefault());
    }

    /// <summary>
    /// Gets an artifact instance by identifier.
    /// </summary>
    public ArtifactInstanceGeneric GetArtifact(ArtifactIdentifier identifier)
    {
        lock (syncRoot)
        {
            return _artifacts[identifier];
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
        lock (syncRoot)
        {
            return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
        }
    }

    /// <summary>
    /// Gets a typed artifact instance by identifier.
    /// </summary>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        lock (syncRoot)
        {
            return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
        }
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

    private static string DescribeArtifact(ArtifactInstanceGeneric instance)
    {
        string reference = Logging.VariableFormatter.Format(instance.Reference);
        string lastVersion = instance.VersionCount == 0
            ? "<no data>"
            : Logging.VariableFormatter.Format(instance.Last);

        return $"ref={reference}; state={instance.State}; versions={instance.VersionCount}; latest={lastVersion}";
    }
}