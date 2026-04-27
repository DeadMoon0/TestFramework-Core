using Newtonsoft.Json;
using System.Collections.Generic;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Stores artifact instances for a timeline run and reports updates to logging and debugging surfaces.
/// </summary>
public class ArtifactStore : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the artifact store has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the artifact store.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

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
        bool replaced = _artifacts.TryGetValue(instance.Identifier, out var previous);
        if (replaced)
        {
            logger.LogInformation(
                "Set Artifact ({0}) {1} -> {2}",
                instance.Identifier,
                DescribeArtifact(previous!),
                DescribeArtifact(instance));
        }
        else
        {
            logger.LogInformation("Set Artifact ({0}) = {1}", instance.Identifier, DescribeArtifact(instance));
        }

        _artifacts[instance.Identifier] = instance;
        debuggingSession.UpdateArtifactAsync(instance.Identifier, GetDebuggingStateFromInstance(instance));
    }

    internal static Debugger.ArtifactState GetDebuggingStateFromInstance(ArtifactInstanceGeneric instance)
    {
        return new Debugger.ArtifactState { Key = instance.Identifier, KindName = instance.Artifact.ToString(), Data = JsonConvert.SerializeObject(instance.VersionCount != 0 ? instance[instance.VersionCount - 1] : null), Reference = JsonConvert.SerializeObject(instance.Reference) };
    }

    /// <summary>
    /// Gets an artifact instance by identifier.
    /// </summary>
    public ArtifactInstanceGeneric GetArtifact(ArtifactIdentifier identifier)
    {
        return _artifacts[identifier];
    }

    /// <summary>
    /// Gets a typed artifact instance by identifier using an explicit kind token.
    /// </summary>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactKind<TArtifactDescriber, TArtifactData, TArtifactReference> kind, ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
    }

    /// <summary>
    /// Gets a typed artifact instance by identifier.
    /// </summary>
    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
    }

    /// <summary>
    /// Returns all artifact instances currently stored for the run.
    /// </summary>
    public IEnumerable<ArtifactInstanceGeneric> GetAll()
    {
        return _artifacts.Values;
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