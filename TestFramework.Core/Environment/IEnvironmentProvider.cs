using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Environment;

/// <summary>
/// Resolves required environment components for a timeline run and exposes the concrete components by identifier.
/// </summary>
public interface IEnvironmentProvider
{
    /// <summary>
    /// Resolves the environment components needed for the provided artifacts and requirements.
    /// </summary>
    /// <param name="artifacts">Artifacts already available for the run.</param>
    /// <param name="requirements">The environment requirements declared by the steps in the run.</param>
    /// <returns>The identifiers of the environment components that should be available for the run.</returns>
    IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements);

    /// <summary>
    /// Gets a concrete environment component by identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the component to resolve.</param>
    EnvComponent GetComponent(EnvComponentIdentifier identifier);
}