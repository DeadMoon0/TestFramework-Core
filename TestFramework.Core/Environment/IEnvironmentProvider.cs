using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Environment;

/// <summary>
/// Resolves required environment components for a timeline run and exposes the concrete components by identifier.
/// </summary>
public interface IEnvironmentProvider
{
    /// <summary>
    /// Gets a value indicating whether dependency-ready environment components may be created in parallel.
    /// </summary>
    bool SupportsParallelComponentCreation => false;

    /// <summary>
    /// Resolves the environment components needed for the provided artifacts and requirements.
    /// </summary>
    /// <param name="artifacts">Artifacts already available for the run.</param>
    /// <param name="requirements">The environment requirements declared by the steps in the run.</param>
    /// <returns>The identifiers of the environment components that should be available for the run.</returns>
    IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements);

    /// <summary>
    /// Whether this environment stands in for every resource of a kind - an emulator taking the place of a
    /// whole service - rather than only for the resources it declares itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It decides which of a run's requirements reach this environment. A requirement for a resource the
    /// environment declared always does. Any other requirement reaches it only when it answers yes for
    /// that requirement's kind: a storage emulator serves every storage account the run names, while an
    /// environment that runs one container per declared API has no business with an API the run reaches
    /// somewhere else - and a run may therefore use both.
    /// </para>
    /// <para>
    /// Yes by default, so an environment written against an earlier engine keeps receiving every
    /// requirement, as it always did.
    /// </para>
    /// </remarks>
    /// <param name="resourceKind">The kind.</param>
    /// <returns>True when every requirement of that kind is this environment's to serve.</returns>
    bool ProvidesEveryResourceOf(string resourceKind) => true;

    /// <summary>
    /// Gets a concrete environment component by identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the component to resolve.</param>
    EnvComponent GetComponent(EnvComponentIdentifier identifier);
}