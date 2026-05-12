using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment.Internal;

internal static class EnvComponentLifecycleRunner
{
    internal static async Task CreateAsync(
        IEnvironmentProvider environment,
        IEnumerable<EnvComponentIdentifier> rootComponents,
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken,
        Action<EnvComponentIdentifier, object?> setState)
    {
        if (!environment.SupportsParallelComponentCreation)
        {
            IReadOnlyList<EnvComponent> orderedComponents = EnvComponentGraph.Order(environment, rootComponents);
            foreach (EnvComponent component in orderedComponents)
            {
                logger.LogInformation("Create EnvComponent ({0})", component.Id);
                object? state = await component.CreateAsync(environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken);
                setState(component.Id, state);
            }

            return;
        }

        IReadOnlyList<IReadOnlyList<EnvComponent>> componentLayers = EnvComponentGraph.Layers(environment, rootComponents);
        foreach (IReadOnlyList<EnvComponent> componentLayer in componentLayers)
        {
            foreach (EnvComponent component in componentLayer)
                logger.LogInformation("Create EnvComponent ({0})", component.Id);

            (EnvComponentIdentifier Id, object? State)[] creationResults = await Task.WhenAll(componentLayer
                .Select(async component => (component.Id, State: await component.CreateAsync(environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken))));

            foreach ((EnvComponentIdentifier componentId, object? state) in creationResults)
                setState(componentId, state);
        }
    }

    internal static async Task DeconstructAsync(
        IEnvironmentProvider environment,
        IReadOnlyList<EnvComponentIdentifier> creationOrder,
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken,
        Func<EnvComponentIdentifier, object?> getState)
    {
        for (int i = creationOrder.Count - 1; i >= 0; i--)
        {
            EnvComponentIdentifier identifier = creationOrder[i];
            EnvComponent component = environment.GetComponent(identifier);
            object? state = getState(identifier);
            logger.LogInformation("Deconstruct EnvComponent ({0})", component.Id);
            await component.DeconstructAsync(state, environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken);
        }
    }
}