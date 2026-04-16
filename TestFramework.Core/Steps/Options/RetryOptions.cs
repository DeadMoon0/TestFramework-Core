using System;
using TestFramework.Core;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

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

public class RetryOptions : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private VariableReference<int> _maxRetryCount = 0;
    public VariableReference<int> MaxRetryCount { get => _maxRetryCount; set { ((IFreezable)this).EnsureNotFrozen(); _maxRetryCount = value; } }

    private VariableReference<CalcDelay> _calcDelay = Var.Const<CalcDelay>((i) => TimeSpan.FromSeconds(Math.Pow(2, i)));
    public VariableReference<CalcDelay> CalcDelay { get => _calcDelay; set { ((IFreezable)this).EnsureNotFrozen(); _calcDelay = value; } }

    public void CloneTo(RetryOptions target)
    {
        target.MaxRetryCount = MaxRetryCount;
        target.CalcDelay = CalcDelay;
    }
}