using System.Linq;
using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
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
    /// Gets how the component participates in cross-run reuse.
    /// </summary>
    public virtual EnvComponentReuseMode ReuseMode => EnvComponentReuseMode.PerRun;

    /// <summary>
    /// Gets the component identifiers that must exist before this component can be created.
    /// </summary>
    public virtual IReadOnlyList<EnvComponentIdentifier> Dependencies => [];

    /// <summary>
    /// Creates the component state.
    /// </summary>
    public abstract Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context);

    /// <summary>
    /// Deconstructs the component state.
    /// </summary>
    public abstract Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context);

    /// <summary>
    /// Whether this component belongs to the run creating it, or was already running.
    /// </summary>
    /// <remarks>
    /// Internal because it is the engine's answer, not a component's. A component declares what it
    /// <em>permits</em> through <see cref="ReuseMode"/>; whether a given run actually borrowed it or created
    /// it depends on whether a persistent context was standing when the run started, which the component
    /// cannot know and should not claim.
    /// </remarks>
    internal virtual EnvComponentScope Scope => EnvComponentScope.Run;

    /// <summary>
    /// The channel this component publishes what it started on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A component always has one - the engine derives a channel per component, in a run and in a persistent
    /// bootstrap alike - so this asks for it that way rather than through the nullable property a step sees.
    /// A step is handed nothing, deliberately: a step that could publish a resource value could point a
    /// passing test at a different system than the one it was written to prove.
    /// </para>
    /// <para>
    /// It exists because the alternative reads harmlessly and is not. Publishing through a null channel is a
    /// no-op, the reader then falls back to whatever an author declared - for a container, a default port -
    /// and the run spends its whole timeout dialling something nothing answers on. That failure looks like a
    /// hang rather than a mistake, and it cost a day. Here it is a sentence instead.
    /// </para>
    /// </remarks>
    /// <param name="context">The context this component was handed.</param>
    /// <returns>The channel.</returns>
    /// <exception cref="FrameworkStateException">The context belongs to something that is not a component.</exception>
    protected static EnvironmentResources PublishOn(RunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Resources ?? throw new FrameworkStateException(
            "An environment component was handed a context with no way to publish what it started. Only the engine creates components, and it always supplies one, so this is a framework fault rather than anything a test can cause.");
    }

    /// <summary>
    /// What one of this component's dependencies returned when the run created it - its network, its database
    /// container, the password it generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the run, because the run is what created it. Environments used to keep these in a table on the
    /// environment instance, since a component had no other way to reach a sibling's state - and an instance two
    /// runs share is a table two runs overwrite: the second run's component built on the first run's network, or
    /// tore down the wrong container.
    /// </para>
    /// <para>
    /// Only a declared dependency may be read. That is what makes the read safe: the engine creates every
    /// dependency before the component that names it, so its state exists by the time this asks - and a component
    /// reaching for something it did not declare would be relying on an order nothing promised.
    /// </para>
    /// </remarks>
    /// <typeparam name="TState">The state's type.</typeparam>
    /// <param name="context">The context this component was handed.</param>
    /// <param name="dependency">One of <see cref="Dependencies"/>.</param>
    /// <returns>The state.</returns>
    /// <exception cref="FrameworkConfigurationException"><paramref name="dependency"/> is not a declared dependency.</exception>
    /// <exception cref="FrameworkStateException">The dependency has no state of that type in this run.</exception>
    protected TState StateOf<TState>(RunContext context, EnvComponentIdentifier dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        if (!Dependencies.Contains(dependency))
        {
            throw new FrameworkConfigurationException(
                $"Environment component '{Id}' reads the state of '{dependency}', which it does not declare as a dependency, so nothing creates it first.")
            {
                RecoverySteps = [$"Add '{dependency}' to {GetType().Name}.Dependencies."],
                AvailableOptions = [.. Dependencies.Select(static declared => declared.ToString())],
            };
        }

        (bool found, object? state) = PublishOn(context).StateOf(dependency);

        if (!found)
            throw new FrameworkStateException($"Environment component '{Id}' depends on '{dependency}', and this run has not created it.");

        return state is TState typed
            ? typed
            : throw new FrameworkStateException(
                $"Environment component '{dependency}' produced {(state is null ? "no state" : $"a {state.GetType().Name}")}, and '{Id}' reads it as a {typeof(TState).Name}.");
    }

    /// <summary>
    /// Returns the component identifier as a string.
    /// </summary>
    public override string ToString() => Id.ToString();
}