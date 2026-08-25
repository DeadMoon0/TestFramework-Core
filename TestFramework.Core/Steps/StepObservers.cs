using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Steps;

/// <summary>
/// The observers watching one run's steps, and the guarantee that they cannot change what a step did.
/// </summary>
/// <remarks>
/// <para>
/// Built once per run from what the caller registered, then told about every attempt. The fan-out lives
/// here rather than in the runner so that the runner has one thing to call and one rule to honour, and so
/// that the rule itself - an observer never decides an outcome - is stated in a single place.
/// </para>
/// <para>
/// Every call is wrapped. Evidence gathering that throws must not be able to turn a red step green, and it
/// must not be able to turn a green step red either: a screenshot that failed to save is a worse thing to
/// report than the failure it was taken for. What an observer throws is logged as a warning and dropped.
/// </para>
/// </remarks>
internal sealed class StepObservers
{
    /// <summary>
    /// How long an observer is given before the run says out loud that it is being held.
    /// </summary>
    /// <remarks>
    /// One number doing two jobs, which is why it is not tighter. It is the deadline on the context an
    /// observer is handed, so evidence gathering has a budget it can consult instead of one it invents;
    /// and it is when this class warns that an observer is still going. Photographing a page is well under
    /// a second even on a slow machine, so overrunning it means something else: either an observer is
    /// wedged - and a warning naming it beats a silent stall - or it is holding the run on purpose, and the
    /// warning is exactly the notice a person watching the output wants.
    /// </remarks>
    internal static readonly TimeSpan EvidenceBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// No observers at all - what a run gets when nobody registered one.
    /// </summary>
    internal static StepObservers None { get; } = new StepObservers([]);

    private readonly IReadOnlyList<IStepObserver> observers;

    private StepObservers(IReadOnlyList<IStepObserver> observers)
    {
        this.observers = observers;
    }

    /// <summary>
    /// Collects the observers a caller registered.
    /// </summary>
    /// <remarks>
    /// Registered as services, resolved once per run, exactly as run debuggers are: an observer is a piece
    /// the caller supplies and the engine drives, so it arrives the way every other such piece does. Both
    /// a single registration and a collection are read, because a container offers both and a caller
    /// should not have to know which one Core happens to ask for.
    /// </remarks>
    /// <param name="serviceProvider">The run's services, or null when there are none.</param>
    /// <returns>The observers, or <see cref="None"/> when nothing is registered.</returns>
    internal static StepObservers For(IServiceProvider? serviceProvider)
    {
        if (serviceProvider is null)
        {
            return None;
        }

        List<IStepObserver> found = [];

        Type collectionType = typeof(IEnumerable<>).MakeGenericType(typeof(IStepObserver));

        if (serviceProvider.GetService(collectionType) is IEnumerable registered)
        {
            foreach (object? candidate in registered)
            {
                Add(found, candidate as IStepObserver);
            }
        }

        Add(found, serviceProvider.GetService(typeof(IStepObserver)) as IStepObserver);

        return found.Count == 0 ? None : new StepObservers([.. found]);
    }

    /// <summary>Tells the observers a step's attempt is starting.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="run">Builds the context to hand over, called only when something is watching.</param>
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    /// <returns>A task that completes when every observer has.</returns>
    internal Task StartingAsync(StepObservation observation, Func<RunContext> run, ScopedLogger logger)
        => this.TellAsync(
            (observer, context) => observer.OnStepStartingAsync(observation, context),
            observation,
            run,
            nameof(IStepObserver.OnStepStartingAsync),
            logger);

    /// <summary>Tells the observers an attempt failed.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="exception">What it threw.</param>
    /// <param name="run">Builds the context to hand over, called only when something is watching.</param>
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    /// <returns>A task that completes when every observer has.</returns>
    internal Task FailedAsync(StepObservation observation, Exception exception, Func<RunContext> run, ScopedLogger logger)
        => this.TellAsync(
            (observer, context) => observer.OnStepFailedAsync(observation, exception, context),
            observation,
            run,
            nameof(IStepObserver.OnStepFailedAsync),
            logger);

    /// <summary>Tells the observers an attempt ran out of time.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="run">Builds the context to hand over, called only when something is watching.</param>
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    /// <returns>A task that completes when every observer has.</returns>
    internal Task TimedOutAsync(StepObservation observation, Func<RunContext> run, ScopedLogger logger)
        => this.TellAsync(
            (observer, context) => observer.OnStepTimedOutAsync(observation, context),
            observation,
            run,
            nameof(IStepObserver.OnStepTimedOutAsync),
            logger);

    private async Task TellAsync(
        Func<IStepObserver, RunContext, Task> call,
        StepObservation observation,
        Func<RunContext> run,
        string hook,
        ScopedLogger logger)
    {
        if (this.observers.Count == 0)
        {
            // The common case by far, and the reason the context arrives as a factory rather than as a
            // context: a run with nothing watching should not build one per step only to drop it.
            return;
        }

        foreach (IStepObserver observer in this.observers)
        {
            try
            {
                // One context each, so two observers cannot spend each other's budget.
                await AwaitAsync(call(observer, run()), observer, hook, logger).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Swallowed on purpose. An observer is told what happened; it does not get a say in it,
                // and that includes failing.
                logger.LogWarning(
                    "{0}.{1} threw while watching '{2}'. The step's own outcome stands.\n{3}",
                    observer.GetType().Name,
                    hook,
                    observation.Label,
                    exception.ToString());
            }
        }
    }

    /// <summary>
    /// Waits for an observer, saying so once it has had longer than its budget.
    /// </summary>
    /// <remarks>
    /// Waited out rather than cut off. Abandoning an observer would make what a run reports depend on how
    /// fast a screenshot was - the same class of mistake as letting one change an outcome - and it would
    /// break the one case where holding the run is the entire point: a browser kept open on a failure for a
    /// person to look at. So the run waits, and says whose work it is waiting on.
    /// </remarks>
    private static async Task AwaitAsync(Task work, IStepObserver observer, string hook, ScopedLogger logger)
    {
        // The timer is cancelled the moment the observer answers, which is the normal case. Left running,
        // every observed step in a run would leave a half-minute timer behind it.
        using CancellationTokenSource answered = new CancellationTokenSource();

        Task budget = Task.Delay(EvidenceBudget, answered.Token);

        if (await Task.WhenAny(work, budget).ConfigureAwait(false) == work)
        {
            await answered.CancelAsync().ConfigureAwait(false);
            await work.ConfigureAwait(false);

            return;
        }

        logger.LogWarning(
            "{0}.{1} has been running for over {2:0} s and is holding the run. Still waiting for it.",
            observer.GetType().Name,
            hook,
            EvidenceBudget.TotalSeconds);

        await work.ConfigureAwait(false);
    }

    private static void Add(List<IStepObserver> observers, IStepObserver? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        // A container that answers both the single and the collection question hands back the same
        // instance twice; watching a step twice would double every screenshot it takes.
        foreach (IStepObserver known in observers)
        {
            if (ReferenceEquals(known, candidate))
            {
                return;
            }
        }

        observers.Add(candidate);
    }
}
