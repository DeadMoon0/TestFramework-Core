using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that a step which never ran still has an outcome after the run, and says why it never ran.
/// </summary>
/// <remarks>
/// The damage is twofold. A step left with no result threw a bare "Sequence contains no elements" when a test
/// read its state; and a run whose whole Main Stage was skipped because it was cancelled between stages had
/// no failed step at all, so <see cref="TimelineRun.EnsureRanToCompletion"/> passed - a cancelled run that
/// read green.
/// </remarks>
public sealed class UnranStepTests
{
    [Fact]
    public void AStepThatHasNotRunSaysSoWhenItsTypedResultIsRead()
    {
        // Every runtime instance is a typed one, so the typed property is what a caller holding a step's
        // instance reaches - and it threw the bare "Sequence contains no elements" the untyped one no longer did.
        StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> instance = new NoOpStep().GetInstance();

        FrameworkStateException refusal = Assert.Throws<FrameworkStateException>(() => instance.LastResult);

        Assert.Contains("has not run", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepOfAStageSkippedAfterAFailedPreSetupIsSkippedAndSaysWhy()
    {
        TimelineRun run = await Timeline.Create()
            .Trigger(new StepWithPreStep(new FailingStep())).Label("needs-pre")
            .Trigger(new NoOpStep()).Label("main")
            .Build()
            .SetupRun()
            .RunAsync();

        Assert.Equal(StepState.Skipped, run.Step("main").State);
        Assert.Contains("failed", run.Step("main").LastResult.SkipReason, StringComparison.Ordinal);
        Assert.Equal(StageState.Skipped, MainStage(run).Result.State);
    }

    [Fact]
    public async Task AStepInALayerAfterAFailedOneIsSkippedAndSaysWhy()
    {
        // The reader reads what the writer writes, so it is planned into the layer after it - and a failed
        // layer ends the stage.
        TimelineRun run = await Timeline.Create()
            .Trigger(new FailingWriterStep()).Label("writer")
            .Trigger(new ReaderStep()).Label("reader")
            .Build()
            .SetupRun()
            .RunAsync();

        Assert.Equal(StepState.Error, run.Step("writer").State);
        Assert.Equal(StepState.Skipped, run.Step("reader").State);
        Assert.Contains("'writer'", run.Step("reader").LastResult.SkipReason, StringComparison.Ordinal);
        Assert.Contains("did not complete", run.Step("reader").LastResult.SkipReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunCancelledBetweenStagesDoesNotReadAsComplete()
    {
        // The stop arrives while Pre-Setup runs, so Pre-Setup finishes cleanly and the Main Stage is skipped
        // whole. No step failed - which is exactly how the run used to pass.
        StoppingDebugger debugger = new();

        TimelineRun run = await Timeline.Create()
            .Trigger(new StepWithPreStep(new StoppingStep(debugger))).Label("needs-pre")
            .Trigger(new NoOpStep()).Label("main")
            .Build()
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        Assert.Equal(StepState.Skipped, run.Step("main").State);
        Assert.Contains("cancelled", run.Step("main").LastResult.SkipReason, StringComparison.OrdinalIgnoreCase);

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);
        Assert.Contains("did not run", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedRunListsTheFailureFirstAndTheStepsItSkippedOnce()
    {
        // The skipped steps belong in the report, but not as a pile on top of the one failure worth reading.
        TimelineRun run = await Timeline.Create()
            .Trigger(new StepWithPreStep(new FailingStep())).Label("needs-pre")
            .Trigger(new NoOpStep()).Label("main")
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        int failed = failure.Message.IndexOf("failed on purpose", StringComparison.Ordinal);
        int skipped = failure.Message.IndexOf("did not run", StringComparison.Ordinal);
        Assert.True(failed >= 0 && skipped > failed, $"Expected the failure before the skipped steps:{System.Environment.NewLine}{failure.Message}");
    }

    private static StageInstance MainStage(TimelineRun run)
        => run.Stages.Single(stage => stage.Stage.Name == "Main Stage");

    private sealed class StoppingDebugger : EmptyRunDebugger, ISupportsRunCancellation
    {
        public event Action<string?>? CancellationRequested;

        public void Stop(string? reason) => CancellationRequested?.Invoke(reason);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class StepWithPreStep(StepGeneric preStep) : Step<EmptyStepResultContext>, IHasPreStep
    {
        public override string Name => "Has a pre-step";
        public override string Description => "Puts its pre-step into Pre-Setup.";
        public override bool DoesReturn => false;

        public StepGeneric? CreatePreStep(VariableStore variableStore) => preStep;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new StepWithPreStep(preStep).WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class StoppingStep(StoppingDebugger debugger) : Step<EmptyStepResultContext>
    {
        public override string Name => "Stops the run";
        public override string Description => "Asks the run to stop, then finishes cleanly.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            debugger.Stop("the test asked to stop");
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new StoppingStep(debugger).WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class FailingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Fails";
        public override string Description => "Fails on purpose.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("the pre-step failed on purpose");

        public override Step<EmptyStepResultContext> Clone() => new FailingStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class FailingWriterStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Fails before writing";
        public override string Description => "Declares a variable it never gets to write.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("the writer failed on purpose");

        public override Step<EmptyStepResultContext> Clone() => new FailingWriterStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) => contract.Output(new VariableIdentifier("shared"));
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class ReaderStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Reads what the writer wrote";
        public override string Description => "Depends on the writer.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new ReaderStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) => contract.Input(new VariableIdentifier("shared"));
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class NoOpStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Does nothing";
        public override string Description => "Exists to be skipped.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new NoOpStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
