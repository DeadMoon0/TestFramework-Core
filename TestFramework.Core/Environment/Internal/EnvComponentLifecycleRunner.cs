using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment.Internal;

internal static class EnvComponentLifecycleRunner
{
    /// <summary>
    /// Creates the components, each given a context it can publish what it started on.
    /// </summary>
    /// <remarks>
    /// The channel is derived per component and named after it, so a value's recorded source is the component
    /// that produced it rather than "the environment". When there is nothing to publish into - a bootstrap
    /// outside any run - components are handed the context unchanged and see a null channel, which is the same
    /// answer they get for anything else that does not apply to them.
    /// </remarks>
    internal static async Task CreateAsync(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> rootComponents, RunContext context, Action<EnvComponentIdentifier, object?, EnvComponentScope> setState, ResourcePublishing? publishing = null)
    {
        if (!environment.SupportsParallelComponentCreation)
        {
            IReadOnlyList<EnvComponent> orderedComponents = EnvComponentGraph.Order(environment, rootComponents);
            foreach (EnvComponent component in orderedComponents)
            {
                object? state = await component.CreateAsync(environment, Publishing(context, publishing, component));
                setState(component.Id, state, component.Scope);
            }

            return;
        }

        IReadOnlyList<IReadOnlyList<EnvComponent>> componentLayers = EnvComponentGraph.Layers(environment, rootComponents);
        foreach (IReadOnlyList<EnvComponent> componentLayer in componentLayers)
        {
            (EnvComponentIdentifier Id, object? State, EnvComponentScope Scope)[] creationResults = await Task.WhenAll(componentLayer
                .Select(async component => (component.Id, State: await component.CreateAsync(environment, Publishing(context, publishing, component)), component.Scope)));

            foreach ((EnvComponentIdentifier componentId, object? state, EnvComponentScope scope) in creationResults)
                setState(componentId, state, scope);
        }
    }

    /// <summary>
    /// The context one component is given: the run, plus a channel to publish on when there is one.
    /// </summary>
    private static RunContext Publishing(RunContext context, ResourcePublishing? publishing, EnvComponent component)
        => publishing is null ? context : context.ForEnvironment(publishing.ChannelFor(component.Id));

    /// <summary>
    /// Takes down what the caller owns, in reverse creation order, and says what it left standing.
    /// </summary>
    /// <remarks>
    /// The scope decides, and it decides out loud. A borrowed component used to be protected only by the
    /// stand-in that replaces it doing nothing when asked to deconstruct - correct, and impossible to see
    /// from here or anywhere else. Skipping it by scope means a run cannot take down something it does not
    /// own even if it reaches the real component, and the run log says which resources outlived it.
    /// </remarks>
    /// <param name="environment">The run's environment.</param>
    /// <param name="creationOrder">Every component the run has, in the order they were created.</param>
    /// <param name="context">The run.</param>
    /// <param name="getState">What each component returned when it was created.</param>
    /// <param name="scopeOf">Whether the caller owns each component. Everything is owned when not given.</param>
    internal static async Task DeconstructAsync(
        IEnvironmentProvider environment,
        IReadOnlyList<EnvComponentIdentifier> creationOrder,
        RunContext context,
        Func<EnvComponentIdentifier, object?> getState,
        Func<EnvComponentIdentifier, EnvComponentScope>? scopeOf = null)
    {
        List<EnvComponentIdentifier> reused = [];

        for (int i = creationOrder.Count - 1; i >= 0; i--)
        {
            EnvComponentIdentifier identifier = creationOrder[i];
            if (scopeOf is not null && scopeOf(identifier) == EnvComponentScope.Reused)
            {
                reused.Add(identifier);
                continue;
            }

            EnvComponent component = environment.GetComponent(identifier);
            object? state = getState(identifier);
            await component.DeconstructAsync(state, environment, context);
        }

        if (reused.Count > 0)
        {
            reused.Reverse();
            context.Logger.LogInformation($"Left {reused.Count} reused environment component(s) standing, because this run did not create them: {string.Join(", ", reused)}.");
        }
    }
}