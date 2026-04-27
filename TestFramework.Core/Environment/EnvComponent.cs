using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

/// <summary>
/// Represents an environment component that can be created and deconstructed for a timeline run.
/// </summary>
public abstract class EnvComponent
{
    /// <summary>
    /// Gets the component identifier.
    /// </summary>
    public abstract EnvComponentIdentifier Id { get; }

    /// <summary>
    /// Gets the component identifiers that must exist before this component can be created.
    /// </summary>
    public virtual IReadOnlyList<EnvComponentIdentifier> Dependencies => [];

    /// <summary>
    /// Creates the component state.
    /// </summary>
    public abstract Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Deconstructs the component state.
    /// </summary>
    public abstract Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the component identifier as a string.
    /// </summary>
    public override string ToString() => Id.ToString();
}