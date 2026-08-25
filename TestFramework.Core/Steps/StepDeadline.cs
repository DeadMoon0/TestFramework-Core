using System;
using System.Threading;

namespace TestFramework.Core.Steps;

/// <summary>
/// How long a step has left, and the token that fires when it runs out.
/// </summary>
/// <remarks>
/// <para>
/// A step used to be told only "you are cancelled", never "you have this long" - so any step that wanted
/// to fail with a useful message had to guess its own margin and stop early. Two packages ship that same
/// workaround today, arrived at independently. Handing the deadline to the step deletes the need for it:
/// a step that knows when it runs out can decide what to do about it, and its own account of what it was
/// waiting for is the one a reader gets.
/// </para>
/// <para>
/// After the deadline fires there is a short grace window in which a step may still return or throw, and
/// what it says then is what surfaces. Past that, the attempt is abandoned - and quarantined, so what it
/// writes afterwards cannot reach a later test.
/// </para>
/// </remarks>
public sealed class StepDeadline
{
    private readonly DateTimeOffset? expiresAt;
    private readonly Func<DateTimeOffset> now;

    internal StepDeadline(TimeSpan total, CancellationToken token, Func<DateTimeOffset>? now = null)
    {
        this.now = now ?? (static () => DateTimeOffset.UtcNow);
        this.Total = total;
        this.Token = token;
        this.expiresAt = IsBounded(total) ? this.now() + total : null;
    }

    /// <summary>
    /// How long the step was given in total, or <see cref="Timeout.InfiniteTimeSpan"/> when unbounded.
    /// </summary>
    public TimeSpan Total { get; }

    /// <summary>
    /// Fires when the time runs out. The same token the step receives, named for what it means.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>Whether this step has a deadline at all.</summary>
    public bool IsUnbounded => this.expiresAt is null;

    /// <summary>
    /// How long is left, floored at zero. <see cref="Timeout.InfiniteTimeSpan"/> when unbounded.
    /// </summary>
    /// <remarks>
    /// Honest at any moment, which is what lets a step decide how patient to be: an inner wait can ask
    /// for what remains instead of a figure somebody guessed at when the step was written.
    /// </remarks>
    public TimeSpan Remaining
    {
        get
        {
            if (this.expiresAt is not { } expiry)
            {
                return Timeout.InfiniteTimeSpan;
            }

            TimeSpan remaining = expiry - this.now();

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// The grace window a step gets after its deadline fires, to say what went wrong.
    /// </summary>
    /// <remarks>
    /// A tenth of the step's own budget, never below a second and never above five: long enough that a
    /// step which was mid-request can finish complaining, short enough that a wedged step does not hold
    /// the suite. Measured margins, not taste - the two packages that hand-rolled this landed in the same
    /// range.
    /// </remarks>
    /// <param name="total">The step's timeout.</param>
    /// <returns>The grace window.</returns>
    public static TimeSpan GraceFor(TimeSpan total)
    {
        if (!IsBounded(total))
        {
            return TimeSpan.Zero;
        }

        double tenth = total.TotalMilliseconds / 10;

        return TimeSpan.FromMilliseconds(Math.Clamp(tenth, 1_000, 5_000));
    }

    private static bool IsBounded(TimeSpan total)
        => total > TimeSpan.Zero && total != Timeout.InfiniteTimeSpan;
}
