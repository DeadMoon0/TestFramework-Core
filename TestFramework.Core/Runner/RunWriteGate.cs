using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Runner;

/// <summary>
/// The one thing that knows whether a run still takes writes.
/// </summary>
/// <remarks>
/// <para>
/// A run's live state is split over several channels - variables, artifacts, run state, effective settings,
/// resource values, environment state and widgets - and each used to answer "has the run finished?" with a flag
/// of its own, flipped by a list of calls at the end of the run that every new channel had to be added to. One
/// gate per run replaces all of them: <see cref="Finish"/> closes every channel at once, and a channel that is
/// added later cannot be forgotten, because writing to it at all takes a <see cref="RunWriteTicket"/> and only
/// this gate's writers mint those.
/// </para>
/// <para>
/// This is the run's lifecycle, not a definition's. A step, a stage or a timeline is frozen when it is built
/// (<see cref="IFreezable"/>); a run is <em>finished</em> when its cleanup has run. The two used to share the word
/// "frozen", which made them look like one rule.
/// </para>
/// </remarks>
internal sealed class RunWriteGate
{
    /// <summary>
    /// How long <see cref="Finish"/> waits for writes it already admitted to land.
    /// </summary>
    /// <remarks>
    /// An admitted write is a few dictionary operations, a file or a pin, so it lands in microseconds; the wait
    /// is what makes the finished run an exact snapshot rather than one a last write can still change. It is
    /// bounded because a pin runs the reference's own code, and a run must not hang on a step it has already
    /// stopped waiting for.
    /// </remarks>
    internal static readonly TimeSpan AdmittedWriteGrace = TimeSpan.FromSeconds(5);

    private int finished;
    private int admitted;

    internal RunWriteGate()
    {
        this.Unrestricted = new RunWriter(this, null, null);
    }

    /// <summary>
    /// A gate that is finished from the start, for values nothing may ever write.
    /// </summary>
    /// <returns>The gate.</returns>
    internal static RunWriteGate Closed()
    {
        RunWriteGate gate = new RunWriteGate();
        gate.finished = 1;
        return gate;
    }

    /// <summary>
    /// The writer for whatever is not one of a step's attempts: the run's own stages, fixtures, observers and
    /// the environment.
    /// </summary>
    internal RunWriter Unrestricted { get; }

    /// <summary>
    /// Whether the run has finished and takes no more writes.
    /// </summary>
    internal bool HasFinished => Volatile.Read(ref this.finished) == 1;

    /// <summary>
    /// The writer for one attempt at a step, which loses its right to write the moment the run stops waiting for it.
    /// </summary>
    /// <param name="attempts">The step's attempts.</param>
    /// <param name="attempt">This attempt.</param>
    /// <returns>The writer.</returns>
    internal RunWriter For(StepAttemptGate attempts, StepAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(attempt);

        return new RunWriter(this, attempts, attempt);
    }

    /// <summary>
    /// Closes every channel of the run, and waits for writes already admitted to land.
    /// </summary>
    /// <param name="logger">Where to say so when an admitted write did not land within <see cref="AdmittedWriteGrace"/>.</param>
    internal void Finish(ScopedLogger logger)
    {
        if (Interlocked.Exchange(ref this.finished, 1) == 1)
            return;

        Stopwatch waited = Stopwatch.StartNew();
        SpinWait spin = default;

        while (Volatile.Read(ref this.admitted) > 0)
        {
            if (waited.Elapsed > AdmittedWriteGrace)
            {
                logger.LogWarning(
                    "The run finished while {0} write(s) it had admitted were still in progress after {1}; whatever they change lands after the run's snapshot.",
                    Volatile.Read(ref this.admitted),
                    AdmittedWriteGrace);
                return;
            }

            spin.SpinOnce();
        }
    }

    internal bool TryEnter()
    {
        Interlocked.Increment(ref this.admitted);

        if (!this.HasFinished)
            return true;

        Interlocked.Decrement(ref this.admitted);
        return false;
    }

    internal void Exit() => Interlocked.Decrement(ref this.admitted);
}

/// <summary>
/// One of a run's channels of live state, and what a refused write to it means.
/// </summary>
internal sealed class RunChannel
{
    private RunChannel(string name, bool isEvidence)
    {
        this.Name = name;
        this.IsEvidence = isEvidence;
    }

    internal static RunChannel Variable { get; } = new("variable", isEvidence: false);

    internal static RunChannel Artifact { get; } = new("artifact", isEvidence: false);

    internal static RunChannel RunState { get; } = new("run state", isEvidence: false);

    internal static RunChannel EffectiveSetting { get; } = new("effective setting", isEvidence: false);

    internal static RunChannel ResourceValue { get; } = new("resource value", isEvidence: false);

    internal static RunChannel EnvironmentState { get; } = new("environment state", isEvidence: false);

    /// <summary>
    /// Widgets are evidence for a person or a debugger, so a refused one is dropped and never thrown: what a
    /// debugging aid does must not change how a run turns out, or what the code around a finished run sees.
    /// </summary>
    internal static RunChannel Widget { get; } = new("widget", isEvidence: true);

    internal string Name { get; }

    /// <summary>
    /// Whether the channel only carries evidence about the run, so a refusal is reported and never thrown.
    /// </summary>
    internal bool IsEvidence { get; }

    public override string ToString() => this.Name;
}

/// <summary>
/// What a context writes the run's live state with.
/// </summary>
/// <remarks>
/// <para>
/// Every channel asks <see cref="TryAdmit"/>, and nothing else, before it changes anything; the rule for a refusal
/// is decided here once:
/// </para>
/// <list type="bullet">
/// <item>An attempt the run stopped waiting for is still running, but nobody listens to it any more: its write is
/// dropped with a warning, whatever the channel.</item>
/// <item>A finished run is a snapshot. A write to it is a mistake in the code making it, so it throws - except on an
/// evidence channel, where it is dropped with a warning instead.</item>
/// </list>
/// </remarks>
internal sealed class RunWriter
{
    private readonly StepAttemptGate? attempts;
    private readonly StepAttempt? attempt;

    internal RunWriter(RunWriteGate gate, StepAttemptGate? attempts, StepAttempt? attempt)
    {
        this.Gate = gate;
        this.attempts = attempts;
        this.attempt = attempt;
    }

    internal RunWriteGate Gate { get; }

    /// <summary>
    /// Asks to write <paramref name="target"/> on <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Which channel of the run's live state.</param>
    /// <param name="target">What is being written, for the message.</param>
    /// <param name="logger">Where a dropped write is reported.</param>
    /// <param name="ticket">The proof the write was admitted; dispose it once the write has landed.</param>
    /// <returns>True when the write may go ahead; false when it was dropped.</returns>
    /// <exception cref="FrameworkStateException">The run has finished and the channel is not an evidence channel.</exception>
    internal bool TryAdmit(RunChannel channel, string target, ScopedLogger logger, [NotNullWhen(true)] out RunWriteTicket? ticket)
    {
        ticket = null;

        if (this.attempts is not null && !this.attempts.Allows(this.attempt))
        {
            logger.LogWarning(
                "A write to {0} '{1}' from {2} was dropped: the run stopped waiting for that attempt.",
                channel,
                target,
                this.attempt?.ToString() ?? "an unknown attempt");

            return false;
        }

        if (!this.Gate.TryEnter())
        {
            if (channel.IsEvidence)
            {
                logger.LogWarning("The {0} '{1}' was not kept: the run had already finished.", channel, target);
                return false;
            }

            throw new FrameworkStateException($"The run has finished, so it takes no new {channel}: '{target}'.")
            {
                RecoverySteps =
                [
                    "Write it inside a step of the run.",
                    "Read what the finished run kept instead: a finished run is a snapshot.",
                ],
            };
        }

        ticket = new Admitted(this.Gate);
        return true;
    }

    /// <summary>
    /// Asks to write, for a channel that has to hand something back and so cannot drop a write quietly.
    /// </summary>
    /// <param name="channel">Which channel of the run's live state.</param>
    /// <param name="target">What is being written, for the message.</param>
    /// <param name="logger">Where the refusal is reported.</param>
    /// <returns>The proof the write was admitted; dispose it once the write has landed.</returns>
    /// <exception cref="FrameworkStateException">The attempt was abandoned, or the run has finished.</exception>
    internal RunWriteTicket Admit(RunChannel channel, string target, ScopedLogger logger)
    {
        if (this.TryAdmit(channel, target, logger, out RunWriteTicket? ticket))
            return ticket;

        throw new FrameworkStateException(
            $"The {channel} '{target}' cannot be created: the run stopped waiting for {this.attempt?.ToString() ?? "this attempt"}.");
    }

    // Legal here and nowhere else: a private nested type can only be constructed by the type that encloses it.
    private sealed class Admitted(RunWriteGate gate) : RunWriteTicket(gate);
}

/// <summary>
/// Proof that a write to a run's live state was admitted by that run's gate.
/// </summary>
/// <remarks>
/// Only <see cref="RunWriter"/> makes one, and every method that changes live state takes one, so a write that does
/// not go through the gate does not compile. Disposing it tells the gate the write has landed.
/// </remarks>
internal abstract class RunWriteTicket : IDisposable
{
    private RunWriteGate? gate;

    private protected RunWriteTicket(RunWriteGate gate) => this.gate = gate;

    public void Dispose() => Interlocked.Exchange(ref this.gate, null)?.Exit();
}
