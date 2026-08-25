using TestFramework.Core.Logging;

namespace TestFramework.Core.Steps;

/// <summary>
/// Whether a write to one of the run's stores still counts, and who is asking.
/// </summary>
/// <remarks>
/// <para>
/// Every store faces the same question - an attempt the runner has stopped waiting for is still running,
/// and it must not reach a store a later test reads - so the answer lives here rather than once per store.
/// Two stores each deciding it separately would be two rules, and the second one to be written is the one
/// that quietly disagrees.
/// </para>
/// <para>
/// A dropped write is logged rather than thrown: the abandoned attempt has nobody left to report to, so
/// the warning is for whoever reads the log afterwards and wonders where the value went.
/// </para>
/// </remarks>
internal sealed class StepWriteLicence
{
    /// <summary>
    /// The licence the run's own store holds: writes that belong to no step - a fixture seeding a
    /// variable, the run publishing its summary - are always honoured.
    /// </summary>
    internal static StepWriteLicence Unrestricted { get; } = new StepWriteLicence(null, null);

    private readonly StepAttemptGate? gate;
    private readonly StepAttempt? writer;

    private StepWriteLicence(StepAttemptGate? gate, StepAttempt? writer)
    {
        this.gate = gate;
        this.writer = writer;
    }

    /// <summary>
    /// The licence a store's per-attempt view holds.
    /// </summary>
    /// <param name="gate">The gate holding the attempt that currently counts.</param>
    /// <param name="attempt">The attempt writing through this view.</param>
    /// <returns>The licence.</returns>
    internal static StepWriteLicence For(StepAttemptGate gate, StepAttempt attempt)
        => new StepWriteLicence(gate, attempt);

    /// <summary>
    /// Whether the write may land, warning when it may not.
    /// </summary>
    /// <param name="logger">The run's logger.</param>
    /// <param name="target">What was being written, for the warning.</param>
    /// <returns>True when the write may land.</returns>
    internal bool Allows(ScopedLogger logger, string target)
    {
        if (this.gate is null || this.gate.Allows(this.writer))
        {
            return true;
        }

        logger.LogWarning(
            "A write to '{0}' from {1} was dropped: the run stopped waiting for that attempt.",
            target,
            this.writer?.ToString() ?? "an unknown attempt");

        return false;
    }
}
