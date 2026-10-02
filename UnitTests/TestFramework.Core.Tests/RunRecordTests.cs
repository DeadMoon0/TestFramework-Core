using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that a package's own record lives and finishes with its run, and never throws into the code writing it.
/// </summary>
/// <remarks>
/// The damage it prevents is a package keeping its own copy of "has the run finished?": a mock's call log used to
/// close on its own teardown, so a run that never reached that teardown left it open, and a late call threw into
/// the system under test's thread.
/// </remarks>
public sealed class RunRecordTests
{
    [Fact]
    public void ARecordKeepsWhatIsAppendedInOrder()
    {
        RunRecord<string> record = RunRecord<string>.Open(RunContext.Detached(), "calls");

        Assert.True(record.Append("first"));
        Assert.True(record.Append("second"));

        Assert.Equal(["first", "second"], record.Snapshot());
        Assert.Equal(2, record.Count);
    }

    [Fact]
    public async Task AFinishedRunDropsANewEntryInsteadOfThrowing()
    {
        await Timeline.Create().Trigger(new KeepingStep()).Name("keeps").Build().SetupRun().RunAsync();

        RunRecord<string> record = KeepingStep.Record!;

        Assert.False(record.Append("after-the-end"));
        Assert.Equal(["during"], record.Snapshot());
    }

    [Fact]
    public void ARecordOutlivesTheAttemptThatOpenedIt()
    {
        // An environment component opens its record inside the run's setup step, and the entries arrive after that
        // step has ended - from the system under test, which no step owns. Written as the opening attempt, every
        // one of them was refused as a write from an abandoned attempt.
        DebuggingRunSession session = new(new EmptyRunDebugger());
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);
        RunWriteGate gate = new();
        VariableStore variables = new(logger, session, gate);
        Artifacts.ArtifactStore artifacts = new(logger, session, gate);
        StepAttemptGate attempts = new();
        StepAttempt setup = attempts.Begin("setup-environment", 1);
        RunContext context = new(
            new EmptyServiceProvider(),
            variables.ForAttempt(attempts, setup),
            artifacts.ForAttempt(attempts, setup),
            logger,
            Environment.Graph.ValueResolution.Empty,
            StepDeadline.Start(System.Threading.Timeout.InfiniteTimeSpan, default),
            setup);
        RunRecord<string> record = RunRecord<string>.Open(context, "calls");

        attempts.End(setup);

        Assert.True(record.Append("from the system under test"));
        Assert.Single(record.Snapshot());
    }

    [Fact]
    public void ASnapshotCannotBeEditedThroughACast()
    {
        // A finished run's record has to stay what it says; an array handed out is one cast away from a rewrite.
        RunRecord<string> record = RunRecord<string>.Open(RunContext.Detached(), "calls");
        record.Append("kept");

        IReadOnlyList<string> snapshot = record.Snapshot();

        Assert.False(snapshot is string[]);
        Assert.Throws<System.NotSupportedException>(() => ((IList<string>)snapshot)[0] = "rewritten");
    }

    private sealed class KeepingStep : Step<EmptyStepResultContext>
    {
        public static RunRecord<string>? Record;

        public override string Name => "Keeps a record";
        public override string Description => "Opens a record and keeps it past the run.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            Record = RunRecord<string>.Open(context, "calls");
            Record.Append("during");
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new KeepingStep().WithClonedOptions(this);
        public override void DeclareIO(Steps.Options.StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
