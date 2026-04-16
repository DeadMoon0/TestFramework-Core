using Newtonsoft.Json;
using System.Collections.Generic;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Artifacts;

public class ArtifactStore : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private readonly FreezableDictionary<ArtifactIdentifier, ArtifactInstanceGeneric> _artifacts = [];
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    internal ArtifactStore(ScopedLogger logger, DebuggingRunSession debuggingSession)
    {
        this.logger = logger;
        this.debuggingSession = debuggingSession;
    }

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

    public ArtifactInstanceGeneric GetArtifact(ArtifactIdentifier identifier)
    {
        return _artifacts[identifier];
    }

    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactKind<TArtifactDescriber, TArtifactData, TArtifactReference> kind, ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
    }

    public ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> GetArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        return (ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>)_artifacts[identifier];
    }

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