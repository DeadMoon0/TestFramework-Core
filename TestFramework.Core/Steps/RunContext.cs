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
    /// Builds a context for code that runs outside a step - a fixture, a persistent environment, or a
    /// step being driven directly by its own unit test.
    /// </summary>
    /// <remarks>
    /// No deadline, because nothing is timing this: the token is honoured, so the work is still
    /// cancellable, but <c>Remaining</c> is unbounded rather than a number somebody invented. No attempt
    /// either, so writes through it are the run's own rather than a step's - which is right, because there
    /// is no attempt for the gate to quarantine.
    /// </remarks>
    /// <param name="services">The registered services.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="artifacts">The artifacts.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="values">
    /// The resource values. Inside a real run this is that run's own resolution; a step being tested on
    /// its own can use <see cref="ValueResolution.Empty"/>.
    /// </param>
    /// <param name="cancellationToken">What stops the work, when anything does.</param>
    /// <param name="timeout">
    /// How long the work has, when the caller has a budget of its own - a persistent environment
    /// bootstrap does. Left out, the deadline is unbounded and only the token stops anything.
    /// </param>
    /// <returns>The context.</returns>
    public static RunContext Ambient(
        IServiceProvider services,
        VariableStore variables,
        ArtifactStore artifacts,
        ScopedLogger logger,
        ValueResolution values,
        System.Threading.CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
        => new RunContext(
            services,
            variables,
            artifacts,
            logger,
            values,
            new StepDeadline(timeout ?? System.Threading.Timeout.InfiniteTimeSpan, cancellationToken),
            attempt: null);
}
