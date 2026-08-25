using System;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps;

/// <summary>
/// Everything a step is given: the run's stores, its logger, what it may ask about resources, and how
/// long it has.
/// </summary>
/// <remarks>
/// <para>
/// One object instead of four parameters, and it closes two gaps at once. A step could not previously
/// learn how long it had, so any step wanting a useful timeout message guessed its own margin; and a step
/// could not ask where a resource ended up, so each package grew its own way of finding out and then
/// needed bridges to reach the others. Both answers now arrive by the same route.
/// </para>
/// <para>
/// Note what is <em>not</em> here as a way in: the service provider carries services - factories, pools,
/// options - and nothing about this run. That separation is what keeps resolving a service from silently
/// depending on which run is asking.
/// </para>
/// </remarks>
public sealed class RunContext
{
    internal RunContext(
        IServiceProvider services,
        VariableStore variables,
        ArtifactStore artifacts,
        ScopedLogger logger,
        ValueResolution values,
        StepDeadline deadline,
        StepAttempt? attempt)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(deadline);

        this.Services = services;
        this.Variables = variables;
        this.Artifacts = artifacts;
        this.Logger = logger;
        this.Values = values;
        this.Deadline = deadline;
        this.Attempt = attempt;
    }

    /// <summary>
    /// The run's registered services. Run-agnostic on purpose: never values, never the deadline.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>The run's variables.</summary>
    public VariableStore Variables { get; }

    /// <summary>The run's artifacts.</summary>
    public ArtifactStore Artifacts { get; }

    /// <summary>The scoped logger for the run.</summary>
    public ScopedLogger Logger { get; }

    /// <summary>
    /// Where the run's resources ended up - the one question, whether a container or a person answered.
    /// </summary>
    public ValueResolution Values { get; }

    /// <summary>How long this step has, and the token that fires when it runs out.</summary>
    public StepDeadline Deadline { get; }

    /// <summary>
    /// Which attempt at this step is running, or null outside a step.
    /// </summary>
    /// <remarks>
    /// Carried so a store can tell an abandoned attempt's writes from a live one's - the writer has to be
    /// able to say who it is, because by the time a zombie writes, the run has moved on.
    /// </remarks>
    public StepAttempt? Attempt { get; }

    /// <summary>
    /// Builds a context for code that runs outside a step - a fixture, a persistent environment.
    /// </summary>
    /// <param name="services">The registered services.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="artifacts">The artifacts.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="values">The resource values.</param>
    /// <returns>The context, with no deadline and no attempt.</returns>
    public static RunContext Ambient(
        IServiceProvider services,
        VariableStore variables,
        ArtifactStore artifacts,
        ScopedLogger logger,
        ValueResolution values)
        => new RunContext(
            services,
            variables,
            artifacts,
            logger,
            values,
            new StepDeadline(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.CancellationToken.None),
            attempt: null);
}
