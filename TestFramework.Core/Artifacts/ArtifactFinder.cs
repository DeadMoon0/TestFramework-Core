using TestFramework.Core.Steps;
using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.Core.Conventions;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents the result of locating a single artifact reference.
/// </summary>
/// <param name="Reference">The located artifact reference.</param>
[ClosedShape("A finder's result is the reference it found; anything more about the artifact belongs on the reference.")]
public record ArtifactFinderResult(ArtifactReferenceGeneric Reference);

/// <summary>
/// Represents the result of locating multiple artifact references.
/// </summary>
/// <param name="ArtifactReferences">The located artifact references.</param>
[ClosedShape("A finder's result is the references it found; anything more belongs on each reference.")]
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
    public abstract Task<ArtifactFinderResult?> FindAsync(RunContext context);

    /// <summary>
    /// Locates multiple artifact references.
    /// </summary>
    public abstract Task<ArtifactFinderResultMulti> FindMultiAsync(RunContext context);
}