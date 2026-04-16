using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

public record ArtifactFinderResult(ArtifactReferenceGeneric Reference);
public record ArtifactFinderResultMulti(ArtifactFinderResult[] ArtifactReferences);

public abstract class ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactFinderGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{

}

public abstract class ArtifactFinderGeneric
{
    public abstract Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken);
    public abstract Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken);
}