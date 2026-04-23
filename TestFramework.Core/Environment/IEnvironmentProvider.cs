using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Environment;

public interface IEnvironmentProvider
{
    IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements);

    EnvComponent GetComponent(EnvComponentIdentifier identifier);
}