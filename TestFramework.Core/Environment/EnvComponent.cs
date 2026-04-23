using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

public abstract class EnvComponent
{
    public abstract EnvComponentIdentifier Id { get; }

    public virtual IReadOnlyList<EnvComponentIdentifier> Dependencies => [];

    public abstract Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    public abstract Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    public override string ToString() => Id.ToString();
}