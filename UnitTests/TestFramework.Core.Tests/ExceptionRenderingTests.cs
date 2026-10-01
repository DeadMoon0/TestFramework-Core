using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that a framework failure, printed, still says what actually went wrong and where.
/// </summary>
/// <remarks>
/// The damage it prevents is advice with the cause cut off. The framework's exceptions render recovery
/// steps on top, which is right - but that rendering used to stop there, so a configuration error wrapping a
/// Docker or SQL failure printed as advice alone, and the error underneath was gone from every log, console and
/// test report that prints an exception.
/// </remarks>
public sealed class ExceptionRenderingTests
{
    [Fact]
    public void AFrameworkFailureStillShowsItsCause()
    {
        FrameworkConfigurationException failure = Caught(() => ThrowWrapped("the daemon refused the connection"));

        string text = failure.ToString();

        Assert.Contains("could not start the container", text, StringComparison.Ordinal);
        Assert.Contains("Recovery:", text, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), text, StringComparison.Ordinal);
        Assert.Contains("the daemon refused the connection", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrameworkFailureStillShowsWhereItWasThrown()
    {
        FrameworkConfigurationException failure = Caught(() => ThrowWrapped("anything"));

        Assert.Contains(nameof(ThrowWrapped), failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdviceStillComesFirst()
    {
        // Recovery is what a reader should see before a stack trace, not under it.
        string text = Caught(() => ThrowWrapped("the daemon refused the connection")).ToString();

        Assert.True(
            text.IndexOf("Recovery:", StringComparison.Ordinal) < text.IndexOf("the daemon refused the connection", StringComparison.Ordinal),
            text);
    }

    [Fact]
    public async Task AFailedRunReportsTheCauseOfAPlainStepException()
    {
        TimelineRun run = await Timeline.Create()
            .Trigger(new WrappingStep()).Name("wraps")
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("the row was locked", failure.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TimeoutException), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GuidanceSetAfterConstructionIsInTheMessage()
    {
        // The steps and options are init properties so the exceptions can gain more without a constructor
        // change; the message has to be built from what was set, not from what the constructor saw.
        FrameworkStateException failure = new("The run has finished.")
        {
            RecoverySteps = ["Create it inside a step."],
            AvailableOptions = ["TryGet"],
        };

        Assert.Contains("Create it inside a step.", failure.Message, StringComparison.Ordinal);
        Assert.Contains("TryGet", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Check the preceding setup", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExceptionWithoutGuidanceKeepsItsTypesAdvice()
        => Assert.NotEmpty(new FrameworkTimeoutException("The container did not start in time.").RecoverySteps);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowWrapped(string cause)
    {
        try
        {
            throw new InvalidOperationException(cause);
        }
        catch (InvalidOperationException inner)
        {
            throw new FrameworkConfigurationException(
                "The environment could not start the container.",
                inner)
            {
                RecoverySteps = ["Check that Docker is running."],
            };
        }
    }

    private static FrameworkConfigurationException Caught(Action action)
    {
        try
        {
            action();
        }
        catch (FrameworkConfigurationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the action to throw.");
    }

    private sealed class WrappingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Wraps a cause";
        public override string Description => "Fails with an exception that has an inner one.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("the update failed", new TimeoutException("the row was locked"));

        public override Step<EmptyStepResultContext> Clone() => new WrappingStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
