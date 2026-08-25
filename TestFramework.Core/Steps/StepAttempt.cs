using System;
using System.Threading;

namespace TestFramework.Core.Steps;

/// <summary>
/// Which attempt at a step is the one whose writes still count.
/// </summary>
/// <remarks>
/// <para>
/// When a step outruns its deadline the runner stops waiting for it, but nothing stops the attempt
/// itself: it keeps running, and it keeps writing to the run's stores. The runner's own log admitted as
/// much. That is how a step abandoned in one test can still be writing while the next one reads - the
/// most plausible explanation for a suite that fails differently under load every time.
/// </para>
/// <para>
/// The fix is a token rather than a lock: each attempt gets one, only the current one is honoured, and an
/// abandoned attempt's writes are dropped with a warning instead of landing. A retry takes a fresh token,
/// which is what makes a retry trustworthy - it can no longer be racing the attempt it replaces.
/// </para>
/// </remarks>
public sealed class StepAttempt
{
    private int abandoned;

    internal StepAttempt(string label, int number)
    {
        this.Label = label;
        this.Number = number;
    }

    /// <summary>The step this attempt belongs to.</summary>
    public string Label { get; }

    /// <summary>Which attempt, counting from one.</summary>
    public int Number { get; }

    /// <summary>Whether the runner has stopped waiting for this attempt.</summary>
    public bool IsAbandoned => Volatile.Read(ref this.abandoned) != 0;

    /// <summary>
    /// Marks this attempt as no longer the one that counts.
    /// </summary>
    internal void Abandon() => Volatile.Write(ref this.abandoned, 1);

    /// <summary>
    /// Reads as <c>'checkout' attempt 2</c>.
    /// </summary>
    /// <returns>The description, for the warning a dropped write produces.</returns>
    public override string ToString() => $"'{this.Label}' attempt {this.Number}";
}

/// <summary>
/// The attempt a store honours writes from.
/// </summary>
/// <remarks>
/// <para>
/// Held by the run's stores rather than by the steps, because the store is what has to refuse: a step
/// that has been abandoned does not know it, and asking it to check would be asking the thing that has
/// stopped listening.
/// </para>
/// <para>
/// Only the runner may start or finish an attempt. Anything else calling <c>Begin</c> would abandon
/// whatever step is running - silently voiding every write it went on to make - so the compiler is what
/// prevents it rather than a rule in a document.
/// </para>
/// </remarks>
public sealed class StepAttemptGate
{
    private StepAttempt? current;

    /// <summary>
    /// Makes an attempt the current one, and abandons whichever was before it.
    /// </summary>
    /// <param name="label">The step's label.</param>
    /// <param name="number">Which attempt.</param>
    /// <returns>The attempt.</returns>
    internal StepAttempt Begin(string label, int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        StepAttempt attempt = new StepAttempt(label, number);
        StepAttempt? previous = Interlocked.Exchange(ref this.current, attempt);

        // A retry must not start before the attempt it replaces has lost its licence to write, or the
        // two race over the same variables.
        previous?.Abandon();

        return attempt;
    }

    /// <summary>
    /// Ends an attempt, so nothing outside a step's own execution is honoured either.
    /// </summary>
    /// <param name="attempt">The attempt that finished.</param>
    internal void End(StepAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        attempt.Abandon();
        Interlocked.CompareExchange(ref this.current, null, attempt);
    }

    /// <summary>
    /// Whether a write from a given attempt still counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The writer has to say who it is. Asking only "is the current attempt abandoned" would let exactly
    /// the write this exists to stop through: by the time a zombie writes, the run has usually moved on,
    /// so the current attempt is a healthy newer one and the zombie would ride in on its licence.
    /// </para>
    /// <para>
    /// Writes that belong to no attempt - a fixture seeding a variable, the run publishing its own
    /// summary - are honoured. The gate fences off abandoned steps; it does not make the store
    /// step-only.
    /// </para>
    /// </remarks>
    /// <param name="writer">The attempt doing the writing, or null when it is not a step's write.</param>
    /// <returns>True when the write may land.</returns>
    public bool Allows(StepAttempt? writer)
    {
        if (writer is null)
        {
            return true;
        }

        return !writer.IsAbandoned && ReferenceEquals(Volatile.Read(ref this.current), writer);
    }
}
