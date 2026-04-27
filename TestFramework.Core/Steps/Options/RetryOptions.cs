using System;
using TestFramework.Core;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Calculates the delay before the next retry attempt.
/// </summary>
/// <param name="currentIteration">The current retry iteration, starting at 1 for the first retry.</param>
public delegate TimeSpan CalcDelay(int currentIteration);

/// <summary>
/// Predefined <see cref="CalcDelay"/> strategies for use with <c>.WithRetry()</c>.
/// <para>
/// <b>Iteration numbering:</b> the first retry is iteration 1, the second is 2, and so on.
/// The initial attempt (before any retry) does not call <see cref="CalcDelay"/>.
/// </para>
/// </summary>
public static class CalcDelays
{
    /// <summary>
    /// Exponential back-off: 2^iteration seconds (2 s, 4 s, 8 s, …).
    /// This is the default when no <see cref="CalcDelay"/> is specified.
    /// </summary>
    public static readonly CalcDelay Exponential = i => TimeSpan.FromSeconds(Math.Pow(2, i));

    /// <summary>
    /// Fixed delay of <paramref name="delay"/> between every retry.
    /// </summary>
    public static CalcDelay Fixed(TimeSpan delay) => _ => delay;

    /// <summary>
    /// Linear back-off: <paramref name="step"/> × iteration (e.g. 5 s, 10 s, 15 s, …).
    /// </summary>
    public static CalcDelay Linear(TimeSpan step) => i => step * i;

    /// <summary>No delay between retries.</summary>
    public static readonly CalcDelay None = _ => TimeSpan.Zero;
}

/// <summary>
/// Configures retry behavior for a step.
/// </summary>
public class RetryOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private VariableReference<int> _maxRetryCount = 0;

    /// <summary>
    /// Gets or sets the maximum number of retries allowed for the step.
    /// </summary>
    public VariableReference<int> MaxRetryCount { get => _maxRetryCount; set { ((IFreezable)this).EnsureNotFrozen(); _maxRetryCount = value; } }

    private VariableReference<CalcDelay> _calcDelay = Var.Const<CalcDelay>((i) => TimeSpan.FromSeconds(Math.Pow(2, i)));

    /// <summary>
    /// Gets or sets the retry delay strategy.
    /// </summary>
    public VariableReference<CalcDelay> CalcDelay { get => _calcDelay; set { ((IFreezable)this).EnsureNotFrozen(); _calcDelay = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(RetryOptions target)
    {
        target.MaxRetryCount = MaxRetryCount;
        target.CalcDelay = CalcDelay;
    }
}