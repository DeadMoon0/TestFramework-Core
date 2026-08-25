using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    internal void Starting(StepObservation observation, ScopedLogger logger)
        => Tell(observer => observer.OnStepStarting(observation), observation, nameof(IStepObserver.OnStepStarting), logger);

    /// <summary>Tells the observers an attempt failed.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="exception">What it threw.</param>
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    internal void Failed(StepObservation observation, Exception exception, ScopedLogger logger)
        => Tell(observer => observer.OnStepFailed(observation, exception), observation, nameof(IStepObserver.OnStepFailed), logger);

    /// <summary>Tells the observers an attempt ran out of time.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="logger">The run's logger, for anything an observer throws.</param>
    internal void TimedOut(StepObservation observation, ScopedLogger logger)
        => Tell(observer => observer.OnStepTimedOut(observation), observation, nameof(IStepObserver.OnStepTimedOut), logger);

    private void Tell(Action<IStepObserver> call, StepObservation observation, string hook, ScopedLogger logger)
    {
        foreach (IStepObserver observer in this.observers)
        {
            try
            {
                call(observer);
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
