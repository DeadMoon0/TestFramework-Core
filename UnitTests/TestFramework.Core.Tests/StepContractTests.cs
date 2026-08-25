using System;
using System.Threading;
using TestFramework.Core.Steps;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The step contract: what a step is told about its own deadline, and whose writes still count once the
/// runner has stopped waiting.
/// </summary>
public class StepContractTests
{
    [Fact]
    public void AStepCanAskHowLongItHasLeft()
    {
        // The whole reason two packages hand-rolled their own margins: a step that knows what remains can
        // decide how patient to be, instead of a figure guessed at when the step was written.
        // Elapsed time rather than a wall clock: the deadline is measured monotonically so it cannot
        // disagree with the timer that cancels the step.
        TimeSpan elapsed = TimeSpan.Zero;
        StepDeadline deadline = new StepDeadline(TimeSpan.FromSeconds(30), CancellationToken.None, () => elapsed);

        Assert.Equal(TimeSpan.FromSeconds(30), deadline.Remaining);
        Assert.False(deadline.HasExpired);

        elapsed = TimeSpan.FromSeconds(25);
        Assert.Equal(TimeSpan.FromSeconds(5), deadline.Remaining);
        Assert.False(deadline.HasExpired);
    }

    [Fact]
    public void RemainingIsFlooredRatherThanNegative()
    {
        // A step asking after its deadline gets "no time", not a negative delay to pass to a wait.
        TimeSpan elapsed = TimeSpan.Zero;
        StepDeadline deadline = new StepDeadline(TimeSpan.FromSeconds(5), CancellationToken.None, () => elapsed);

        elapsed = TimeSpan.FromSeconds(30);

        Assert.Equal(TimeSpan.Zero, deadline.Remaining);

        // And it says the time ran out, which is the question a cancelled step actually asks.
        Assert.True(deadline.HasExpired);
    }

    [Fact]
    public void AStepWithNoTimeoutSaysSoRatherThanPretendingToACountdown()
    {
        StepDeadline unbounded = new StepDeadline(Timeout.InfiniteTimeSpan, CancellationToken.None);

        Assert.True(unbounded.IsUnbounded);
        Assert.Equal(Timeout.InfiniteTimeSpan, unbounded.Remaining);
        Assert.Equal(TimeSpan.Zero, StepDeadline.GraceFor(Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void TheGraceWindowIsATenthOfTheBudgetWithinItsClamp()
    {
        // Long enough that a step mid-request can finish complaining, short enough that a wedged step
        // does not hold the suite.
        Assert.Equal(TimeSpan.FromSeconds(3), StepDeadline.GraceFor(TimeSpan.FromSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(1), StepDeadline.GraceFor(TimeSpan.FromSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(5), StepDeadline.GraceFor(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void OnlyTheAttemptTheRunIsWaitingForMayWrite()
    {
        StepAttemptGate gate = new StepAttemptGate();

        StepAttempt first = gate.Begin("checkout", 1);
        Assert.True(gate.Allows(first));

        // A retry starts: the attempt it replaces loses its licence before the new one runs, so the two
        // cannot race over the same variables.
        StepAttempt second = gate.Begin("checkout", 2);

        Assert.False(gate.Allows(first));
        Assert.True(gate.Allows(second));
    }

    [Fact]
    public void AZombieCannotRideInOnALaterAttemptsLicence()
    {
        // The bug the naive gate would have kept: by the time an abandoned attempt writes, the run has
        // moved on, so the current attempt is a healthy newer one.
        StepAttemptGate gate = new StepAttemptGate();
        StepAttempt abandoned = gate.Begin("checkout", 1);

        gate.Begin("next-step", 1);

        Assert.False(gate.Allows(abandoned));
    }

    [Fact]
    public void WritesThatBelongToNoAttemptAreAlwaysHonoured()
    {
        // A fixture seeding a variable, or the run publishing its own summary. The gate fences off
        // abandoned steps; it does not make the store step-only.
        StepAttemptGate gate = new StepAttemptGate();
        gate.Begin("checkout", 1);

        Assert.True(gate.Allows(writer: null));
    }

    [Fact]
    public void AFinishedAttemptStopsCounting()
    {
        // Nothing a step left running in the background gets to write after the step returned either.
        StepAttemptGate gate = new StepAttemptGate();
        StepAttempt attempt = gate.Begin("checkout", 1);

        gate.End(attempt);

        Assert.False(gate.Allows(attempt));
        Assert.True(gate.Allows(writer: null));
    }

    [Fact]
    public void AnAbandonedAttemptSaysWhichOneItWas()
        => Assert.Equal("'checkout' attempt 2", new StepAttemptGate().Begin("checkout", 2).ToString());
}
