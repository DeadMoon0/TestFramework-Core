using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Conventions;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// The two pieces of the framework that were pure discipline: cloning a step definition and freezing one.
/// </summary>
/// <remarks>
/// Dozens of hand-written <c>Clone()</c> overrides and hand-written freeze guards, each a line somebody
/// could forget, with nothing noticing when they did. A run now checks the clone it was given, and a
/// package's suite can check its own assembly.
/// </remarks>
public class StepDisciplineTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInCoreClonesItself()
    {
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(StepGeneric).Assembly);

        output.WriteLine(report.ToString());
        Assert.True(report.Checked > 0, "the check found no steps at all, so it proved nothing");
    }

    [Fact]
    public void FreezingCascadesThroughCoresOwnParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(StepGeneric).Assembly);

        // The skipped list is the honest half: these are the types nothing can construct without the
        // framework's own arguments, and they are covered where they are used instead.
        output.WriteLine(report.ToString());
        foreach (string skipped in report.Skipped)
        {
            output.WriteLine($"  skipped {skipped}");
        }

        Assert.True(report.Checked > 0, "the check constructed nothing, so it proved nothing");
    }

    [Fact]
    public void AFrozenStepsLabelCannotChangeEither()
    {
        // The omission that made the case for finding the options rather than listing them: LabelOptions
        // was the one part missing from StepGeneric.Freeze(), so a settled step could still be renamed -
        // and a renamed step is a different step to every log and every debugger reading the run.
        WellBehavedStep step = new WellBehavedStep();

        step.Freeze();

        Assert.Throws<FrameworkStateException>(() => step.LabelOptions.Label = "renamed after the fact");
    }

    [Fact]
    public async Task AStepThatClonesItselfIntoTheSameInstanceIsRefused()
    {
        // Damage: the modifiers would edit the object the test author is holding, so running the same
        // timeline twice would start the second run already configured by the first.
        Timeline timeline = Timeline.Create()
            .Trigger(new SelfReturningStep()).Name("shares-itself")
            .Build();

        FrameworkConfigurationException failure = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline.SetupRun(outputHelper: output).RunAsync());

        Assert.Contains("returned the same instance", failure.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SelfReturningStep), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepThatInheritsItsBaseClassesCloneIsRefused()
    {
        // Damage: the step runs as its base class and silently loses whatever the subclass added, which
        // looks like the step simply not doing its job.
        Timeline timeline = Timeline.Create()
            .Trigger(new ForgetfulSubclassStep()).Name("collapses")
            .Build();

        FrameworkConfigurationException failure = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline.SetupRun(outputHelper: output).RunAsync());

        Assert.Contains("would run as its base class", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepThatHandsItsOptionsToTheCopyIsRefused()
    {
        // Damage: two steps, one options object. Configuring either configures both, and freezing either
        // freezes both - so the second run fails on a write the first one allowed.
        Timeline timeline = Timeline.Create()
            .Trigger(new OptionSharingStep()).Name("shares-options")
            .Build();

        FrameworkConfigurationException failure = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline.SetupRun(outputHelper: output).RunAsync());

        Assert.Contains("shares its", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProperCloneIsLeftAlone()
    {
        // The positive half. A guard that also refused correct clones would be worse than none.
        Timeline timeline = Timeline.Create()
            .Trigger(new WellBehavedStep()).Name("fine")
            .Build();

        TimelineRun first = await timeline.SetupRun(outputHelper: output).RunAsync();
        TimelineRun second = await timeline.SetupRun(outputHelper: output).RunAsync();

        Assert.Equal(StepState.Complete, first.Step("fine").LastResult.State);
        Assert.Equal(StepState.Complete, second.Step("fine").LastResult.State);
    }

    private class WellBehavedStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Well behaved";

        public override string Description => "Clones itself properly.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new WellBehavedStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    /// <summary>Returns itself, the cheapest wrong clone there is.</summary>
    private sealed class SelfReturningStep : WellBehavedStep
    {
        public override Step<EmptyStepResultContext> Clone() => this;
    }

    /// <summary>Adds behaviour and forgets to override Clone, so its base class answers for it.</summary>
    private sealed class ForgetfulSubclassStep : WellBehavedStep
    {
        public override string Name => "Forgetful subclass";
    }

    /// <summary>Copies the object but hands the copy its own options.</summary>
    private sealed class OptionSharingStep : WellBehavedStep
    {
        public override Step<EmptyStepResultContext> Clone()
            => new OptionSharingStep { RetryOptions = this.RetryOptions }.WithClonedOptions(this);
    }
}
