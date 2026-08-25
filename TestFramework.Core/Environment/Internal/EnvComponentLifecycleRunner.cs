using TestFramework.Core.Steps;
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
    internal static async Task CreateAsync(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> rootComponents, RunContext context, Action<EnvComponentIdentifier, object?> setState)
    {
        if (!environment.SupportsParallelComponentCreation)
        {
            IReadOnlyList<EnvComponent> orderedComponents = EnvComponentGraph.Order(environment, rootComponents);
            foreach (EnvComponent component in orderedComponents)
            {
                object? state = await component.CreateAsync(environment, context);
                setState(component.Id, state);
            }

            return;
        }

        IReadOnlyList<IReadOnlyList<EnvComponent>> componentLayers = EnvComponentGraph.Layers(environment, rootComponents);
        foreach (IReadOnlyList<EnvComponent> componentLayer in componentLayers)
        {
            (EnvComponentIdentifier Id, object? State)[] creationResults = await Task.WhenAll(componentLayer
                .Select(async component => (component.Id, State: await component.CreateAsync(environment, context))));

            foreach ((EnvComponentIdentifier componentId, object? state) in creationResults)
                setState(componentId, state);
        }
    }

    internal static async Task DeconstructAsync(IEnvironmentProvider environment, IReadOnlyList<EnvComponentIdentifier> creationOrder, RunContext context, Func<EnvComponentIdentifier, object?> getState)
    {
        for (int i = creationOrder.Count - 1; i >= 0; i--)
        {
            EnvComponentIdentifier identifier = creationOrder[i];
            EnvComponent component = environment.GetComponent(identifier);
            object? state = getState(identifier);
            await component.DeconstructAsync(state, environment, context);
        }
    }
}