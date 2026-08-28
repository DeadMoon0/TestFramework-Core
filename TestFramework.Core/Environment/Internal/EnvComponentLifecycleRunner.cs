using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
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
            // Started together, recorded one by one. Task.WhenAll would throw away the successful
            // siblings' states when one component in the layer fails - containers that started, that
            // nothing recorded, and that teardown therefore can never see. Every state that exists is
            // recorded before the layer's failure is rethrown, so what did start is what gets torn down.
            // Starting is guarded too: a component that throws synchronously from CreateAsync would
            // otherwise take the layer down before its already-started siblings were even collected.
            List<(EnvComponent Component, Task<object?> Creation)> creations = [];
            List<Exception> failures = [];

            foreach (EnvComponent component in componentLayer)
            {
                try
                {
                    creations.Add((component, component.CreateAsync(environment, Publishing(context, publishing, component))));
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            foreach ((EnvComponent component, Task<object?> creation) in creations)
            {
                try
                {
                    setState(component.Id, await creation, component.Scope);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();

            if (failures.Count > 1)
                throw new AggregateException($"{failures.Count} environment components in one layer failed to create.", failures);
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
        List<(EnvComponentIdentifier Identifier, Exception Failure)> failed = [];

        for (int i = creationOrder.Count - 1; i >= 0; i--)
        {
            EnvComponentIdentifier identifier = creationOrder[i];
            if (scopeOf is not null && scopeOf(identifier) == EnvComponentScope.Reused)
            {
                reused.Add(identifier);
                continue;
            }

            // Isolated per component, the way the artifact teardown next door already is: one component
            // that cannot come down must not stop the ones created before it from being taken down - they
            // are the remaining teardown, and skipping them is how a single failure leaks a whole
            // environment while the cleanup step reads as done.
            try
            {
                EnvComponent component = environment.GetComponent(identifier);
                object? state = getState(identifier);
                await component.DeconstructAsync(state, environment, context);
            }
            catch (Exception exception)
            {
                failed.Add((identifier, exception));
                context.Logger.LogError("Could not deconstruct environment component '{0}'; continuing with the components before it.\n{1}", identifier, exception.ToString());
            }
        }

        if (reused.Count > 0)
        {
            reused.Reverse();
            context.Logger.LogInformation($"Left {reused.Count} reused environment component(s) standing, because this run did not create them: {string.Join(", ", reused)}.");
        }

        // Every component got its chance; now the failures surface as one account instead of the first
        // one hiding the rest. The cleanup step ignores exceptions by design, so this reaches the log and
        // the debugger's record rather than failing the run - but it is a real exception, not a warning,
        // because resources this run created are still standing.
        if (failed.Count > 0)
        {
            throw new FrameworkStateException(
                $"{failed.Count} of {creationOrder.Count} environment component(s) could not be deconstructed and may still be running: "
                + $"{string.Join(", ", failed.Select(static entry => $"'{entry.Identifier}'"))}. Each failure is logged above.");
        }
    }
}