using System;
using System.Diagnostics;
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
    private readonly Func<TimeSpan> elapsed;
    private readonly bool bounded;

    /// <summary>
    /// Starts the clock for a step's deadline.
    /// </summary>
    /// <remarks>
    /// Time is measured with <see cref="Stopwatch"/> rather than the wall clock, and that is not a
    /// preference. <c>DateTimeOffset.UtcNow</c> advances in system timer ticks - about 15 ms on Windows -
    /// while the cancellation this deadline shares fires from a timer of its own. Compared against a coarse
    /// clock, a 200 ms deadline could fire while the clock still read "not yet", so a step asking whether
    /// its time was up got the wrong answer roughly one run in several. A monotonic elapsed time cannot
    /// disagree with a timer that never fires early.
    /// </remarks>
    /// <param name="total">How long the step has.</param>
    /// <param name="token">The token that fires when it runs out.</param>
    /// <param name="elapsed">How much time has passed, for a test that needs to control it.</param>
    internal StepDeadline(TimeSpan total, CancellationToken token, Func<TimeSpan>? elapsed = null)
    {
        Stopwatch started = Stopwatch.StartNew();

        this.elapsed = elapsed ?? (() => started.Elapsed);
        this.Total = total;
        this.Token = token;
        this.bounded = IsBounded(total);
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
    public bool IsUnbounded => !this.bounded;

    /// <summary>
    /// Whether the time ran out.
    /// </summary>
    /// <remarks>
    /// The question a cancelled step actually needs answered: was I stopped because my time is up, or
    /// because the whole run was cancelled? Those deserve different reports - "the file never appeared" is
    /// true of the first and misleading about the second - and a step cannot tell them apart from the
    /// token alone.
    /// <para>
    /// Reading <c>Remaining == TimeSpan.Zero</c> instead looks equivalent and is not: the runner arms the
    /// cancellation and builds this from the same timeout, so the two can only agree if the deadline is
    /// created first. It is, and this is the property that says so out loud rather than leaving every
    /// caller to do the arithmetic and get the edge wrong.
    /// </para>
    /// </remarks>
    public bool HasExpired => this.bounded && this.elapsed() >= this.Total;

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
            if (!this.bounded)
            {
                return Timeout.InfiniteTimeSpan;
            }

            TimeSpan remaining = this.Total - this.elapsed();

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
