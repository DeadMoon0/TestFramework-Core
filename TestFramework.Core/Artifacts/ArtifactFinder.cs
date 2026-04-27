using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents the result of locating a single artifact reference.
/// </summary>
/// <param name="Reference">The located artifact reference.</param>
public record ArtifactFinderResult(ArtifactReferenceGeneric Reference);

/// <summary>
/// Represents the result of locating multiple artifact references.
/// </summary>
/// <param name="ArtifactReferences">The located artifact references.</param>
public record ArtifactFinderResultMulti(ArtifactFinderResult[] ArtifactReferences);

/// <summary>
/// Represents a typed artifact finder.
/// </summary>
public abstract class ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactFinderGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{

}

/// <summary>
/// Represents the non-generic base contract for artifact finders.
/// </summary>
public abstract class ArtifactFinderGeneric
{
    /// <summary>
    /// Locates a single artifact reference.
    /// </summary>
    public abstract Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Locates multiple artifact references.
    /// </summary>
    public abstract Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken);
}