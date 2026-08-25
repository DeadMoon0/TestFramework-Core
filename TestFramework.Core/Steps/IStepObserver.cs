using System;
using System.Threading.Tasks;

namespace TestFramework.Core.Steps;

/// <summary>
/// What a step was, when something happened to it.
/// </summary>
/// <param name="Label">The step's label, or its name when it has none.</param>
/// <param name="Name">The step's name.</param>
/// <param name="StageName">The stage it ran in.</param>
/// <param name="Attempt">Which attempt this was, counting from one.</param>
public sealed record StepObservation(string Label, string Name, string StageName, int Attempt);

/// <summary>
/// Watches steps run, so evidence gathering lives beside the run rather than inside every step.
/// </summary>
/// <remarks>
/// <para>
/// The framework had no hook here, so each package that wanted evidence on failure - a screenshot, the
/// page markup, a held-open browser - wrapped its own <c>Execute</c> in a try/catch and did the work
/// there. That is per-package code for a per-run concern, and the next package copies it.
/// </para>
/// <para>
/// An observer is registered once - as a service, the way a run debugger is - and called for every step
/// the run executes, the ones the framework inserts included: an artifact teardown that fails is exactly
/// when evidence is worth having. Deciding which of those are interesting is the observer's business.
/// </para>
/// <para>
/// It is told what happened; it does not get to change the outcome, because evidence gathering must never
/// be able to turn a red step green - nor a green step red, since a screenshot that failed to save is a
/// worse thing to report than the run it was watching. Anything an observer throws is logged and dropped.
/// </para>
/// <para>
/// Every hook is asynchronous, and that is not politeness. Evidence is I/O by nature - a screenshot, the
/// page's markup, an upload - and a synchronous socket would have forced the first real implementer to
/// block on it inside the runner. It also lets an observer deliberately hold the run: a browser held open
/// on a failure for a person to look at is the point of that feature, not a hang.
/// </para>
/// </remarks>
public interface IStepObserver
{
    /// <summary>Called before a step's attempt begins.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="run">The run, as described on <see cref="OnStepFailedAsync"/>.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnStepStartingAsync(StepObservation observation, RunContext run);

    /// <summary>Called when an attempt failed.</summary>
    /// <remarks>
    /// <para>
    /// Not called for an exception the step's error handling was told to ignore: that attempt threw, but
    /// it did not fail, and an observer capturing evidence for every swallowed exception would bury the
    /// real failures in it.
    /// </para>
    /// <para>
    /// The context is the <em>run's</em>, not the failed step's, and both differences are deliberate. It
    /// carries no attempt, so what an observer writes is the run's own and cannot be discarded along with
    /// the attempt that just died; and it has a budget of its own, because the step's is exactly what ran
    /// out. Everything else - the services, the stores, the logger, where the run's resources ended up -
    /// is the same run the step was looking at, which is how an observer finds what it needs to
    /// photograph.
    /// </para>
    /// </remarks>
    /// <param name="observation">Which step.</param>
    /// <param name="exception">What it threw.</param>
    /// <param name="run">The run the step was part of.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnStepFailedAsync(StepObservation observation, Exception exception, RunContext run);

    /// <summary>Called when an attempt ran out of time, whether or not it answered in the grace window.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="run">The run, as described on <see cref="OnStepFailedAsync"/>.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnStepTimedOutAsync(StepObservation observation, RunContext run);
}
