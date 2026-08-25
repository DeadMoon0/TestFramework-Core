using System;

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
/// An observer is registered once and called for every step. It is told what happened; it does not get to
/// change the outcome, because evidence gathering must never be able to turn a red step green. Anything an
/// observer throws is logged and swallowed for the same reason.
/// </para>
/// </remarks>
public interface IStepObserver
{
    /// <summary>Called before a step's attempt begins.</summary>
    /// <param name="observation">Which step.</param>
    void OnStepStarting(StepObservation observation);

    /// <summary>Called when an attempt threw.</summary>
    /// <param name="observation">Which step.</param>
    /// <param name="exception">What it threw.</param>
    void OnStepFailed(StepObservation observation, Exception exception);

    /// <summary>Called when an attempt ran out of time, whether or not it answered in the grace window.</summary>
    /// <param name="observation">Which step.</param>
    void OnStepTimedOut(StepObservation observation);
}
