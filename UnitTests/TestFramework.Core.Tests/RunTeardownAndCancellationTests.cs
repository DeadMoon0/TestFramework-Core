using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a run does when things go wrong late: a consumer stopping it, a cleanup step failing, a
/// component refusing to come down. None of this shows up in a green run, which is why each case here
/// was found by reading the runner rather than by a failing suite.
/// </summary>
public class RunTeardownAndCancellationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AStoppedRunSaysStoppedNotTimedOut_AndStillTearsDown()
    {
        // A consumer stop and a step timeout arrive through one token, and the frozen record used to say
        // "timed out after 10 minutes" for both - a false statement about a run somebody stopped after
        // seconds. And the stop must still reach teardown with a live token: reaching the cleanup stage
        // only to hand every hook a fired token is reaching teardown in name only.
        CallLog calls = new();
        StoppingDebugger debugger = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Timeline timeline = Timeline.Create()
            .Trigger(new BlockUntilCancelledStep(started)).Name("blocked")
            .Build();

        Task<TimelineRun> running = timeline
            .SetupRun(new DebuggerServiceProvider(debugger), output)
            .SetEnv(new TwoComponentEnvironment(calls))
            .RunAsync();

        await started.Task;
        debugger.Stop("the test asked");

        TimelineRun run = await running;
        StepResultGeneric result = ((StepInstanceGeneric)run.Step("blocked")).RetryResults.Last();

        Assert.Equal(StepState.Error, result.State);
        Assert.NotNull(result.Exception);
        Assert.Contains("was stopped because the run was cancelled", result.Exception!.Message, StringComparison.Ordinal);
        Assert.Contains("the test asked", result.Exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("timed out", result.Exception.Message, StringComparison.OrdinalIgnoreCase);

        // Both components came down, and their deconstruct hooks each checked the token they were given -
        // so this fails if teardown inherits the stop signal.
        Assert.Contains("deconstruct:first", calls.Snapshot());
        Assert.Contains("deconstruct:second", calls.Snapshot());
    }

    [Fact]
    public async Task AFailingUserCleanupStepDoesNotCancelTheRestOfTeardown()
    {
        // Cleanup steps run in sequential layers ahead of the engine's own deconstruct steps, and a failed
        // layer used to end the stage - so one red user cleanup step silently skipped the artifact and
        // environment teardown, which are exactly the layers that still owed somebody work.
        CallLog calls = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new StepWithFailingCleanup()).Name("owner")
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .SetEnv(new TwoComponentEnvironment(calls))
            .RunAsync();

        // The cleanup step's failure is still a failure - it is not swallowed to keep the stage green.
        Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("deconstruct:first", calls.Snapshot());
        Assert.Contains("deconstruct:second", calls.Snapshot());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AnImpossibleTimeoutIsASentenceNotACrash(int seconds)
    {
        // Zero used to arm a token that fires instantly under a deadline reporting unbounded; negative
        // used to throw while arming the timer, outside every catch - which aborted the run with no
        // teardown and no run object to inspect.
        CallLog calls = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NeverRunsStep()).Name("misconfigured").WithTimeOut(TimeSpan.FromSeconds(seconds))
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .SetEnv(new TwoComponentEnvironment(calls))
            .RunAsync();

        StepResultGeneric result = ((StepInstanceGeneric)run.Step("misconfigured")).RetryResults.Last();

        Assert.Equal(StepState.Error, result.State);
        FrameworkConfigurationException refusal = Assert.IsType<FrameworkConfigurationException>(result.Exception);
        Assert.Contains("which no step can run under", refusal.Message, StringComparison.Ordinal);

        Assert.Contains("deconstruct:first", calls.Snapshot());
        Assert.Contains("deconstruct:second", calls.Snapshot());
    }

    [Fact]
    public async Task AFailedPreSetupSkipsTheMainStageInsteadOfBuryingIt()
    {
        // A Pre-Setup that failed used to be followed by the Main Stage anyway, every step of which then
        // failed against whatever was never set up - and the one failure worth reading sat under the pile.
        RecordingStep mainStep = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new StepWithFailingPreStep()).Name("needs-pre")
            .Trigger(mainStep).Name("recorder")
            .Build();

        RecordingStep.Executed = false;
        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("the pre-step failed on purpose", failure.ToString(), StringComparison.Ordinal);
        Assert.False(RecordingStep.Executed, "the main stage ran although its pre-setup had already failed");
    }

    [Fact]
    public async Task ADependencysTimeoutIsAFailureNotAStepTimeout()
    {
        // A TimeoutException thrown while the step still has time is a dependency's timeout - an HTTP
        // client's, a driver's. Classifying it by exception type alone booked it as the step running out
        // of time, which misfiles the failure and calls the wrong evidence hook.
        Timeline timeline = Timeline.Create()
            .Trigger(new DependencyTimeoutStep()).Name("caller").WithTimeOut(TimeSpan.FromMinutes(5))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        StepResultGeneric result = ((StepInstanceGeneric)run.Step("caller")).RetryResults.Last();

        Assert.Equal(StepState.Error, result.State);
        Assert.IsType<TimeoutException>(result.Exception);
        Assert.Contains("the dependency gave up", result.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AComponentThatCannotComeDownDoesNotStopTheOthers()
    {
        // Teardown walks creation order backwards, and the first failing component used to end the loop -
        // every component created before it stayed up, while the cleanup step read as done.
        CallLog calls = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .SetEnv(new FailingTeardownEnvironment(calls))
            .RunAsync();

        // The engine's deconstruct step ignores exceptions by design, so the run itself stays green -
        // the failure reaches the log and the debug record instead.
        run.EnsureRanToCompletion();

        Assert.Contains("deconstruct-failed:second", calls.Snapshot());
        Assert.Contains("deconstruct:first", calls.Snapshot());
    }

    [Fact]
    public async Task AFailingSiblingDoesNotHideWhatStarted()
    {
        // Components in one layer are created together, and Task.WhenAll used to discard the successful
        // siblings' states when one of them threw - containers that started, that nothing recorded, and
        // that teardown therefore could never see.
        CallLog calls = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .SetEnv(new FailingSiblingEnvironment(calls))
            .RunAsync();

        // The layer failed, so the run failed - but the sibling that did start is recorded and comes down.
        Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);
        Assert.True(run.EnvironmentContext.TryGetState<string>("survivor", out string? state));
        Assert.Equal("state:survivor", state);
        Assert.Contains("deconstruct:survivor", calls.Snapshot());
    }

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

    private sealed class CallLog
    {
        private readonly List<string> entries = [];

        public void Add(string entry)
        {
            lock (entries)
                entries.Add(entry);
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (entries)
                return [.. entries];
        }
    }

    private sealed class TwoComponentEnvironment(CallLog calls) : EnvironmentProviderBase
    {
        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
        {
            this.AddComponent(new TokenCheckingComponent("first", calls));
            this.AddComponent(new TokenCheckingComponent("second", calls));

            return [new EnvComponentIdentifier("first"), new EnvComponentIdentifier("second")];
        }
    }

    /// <summary>A component whose teardown refuses a fired token, the way real I/O does.</summary>
    private sealed class TokenCheckingComponent(string id, CallLog calls) : EnvComponent
    {
        public override EnvComponentIdentifier Id => id;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            calls.Add($"create:{id}");
            return Task.FromResult<object?>($"state:{id}");
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
        {
            // Real teardown passes its token to real I/O; a fired one aborts the work. This is that,
            // reduced to the check.
            context.Deadline.Token.ThrowIfCancellationRequested();
            calls.Add($"deconstruct:{id}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTeardownEnvironment(CallLog calls) : EnvironmentProviderBase
    {
        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
        {
            this.AddComponent(new TokenCheckingComponent("first", calls));
            this.AddComponent(new UndeconstructableComponent("second", calls));

            return [new EnvComponentIdentifier("first"), new EnvComponentIdentifier("second")];
        }
    }

    private sealed class UndeconstructableComponent(string id, CallLog calls) : EnvComponent
    {
        public override EnvComponentIdentifier Id => id;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            calls.Add($"create:{id}");
            return Task.FromResult<object?>($"state:{id}");
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
        {
            calls.Add($"deconstruct-failed:{id}");
            throw new InvalidOperationException("this component refuses to come down");
        }
    }

    private sealed class FailingSiblingEnvironment(CallLog calls) : EnvironmentProviderBase
    {
        public override bool SupportsParallelComponentCreation => true;

        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
        {
            this.AddComponent(new TokenCheckingComponent("survivor", calls));
            this.AddComponent(new StillbornComponent("casualty"));

            return [new EnvComponentIdentifier("survivor"), new EnvComponentIdentifier("casualty")];
        }
    }

    private sealed class StillbornComponent(string id) : EnvComponent
    {
        public override EnvComponentIdentifier Id => id;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
            => throw new InvalidOperationException("this component fails to start");

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }

    private sealed class BlockUntilCancelledStep(TaskCompletionSource started) : Step<EmptyStepResultContext>
    {
        public override string Name => "Blocks until cancelled";
        public override string Description => "Signals that it started, then waits for its token.";
        public override bool DoesReturn => false;

        public override async Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.Deadline.Token);
            return EmptyStepResultContext.Instance;
        }

        public override Step<EmptyStepResultContext> Clone() => new BlockUntilCancelledStep(started).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class NoOpStep : Step<EmptyStepResultContext>
    {
        public override string Name => "NoOp";
        public override string Description => "NoOp";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new NoOpStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    /// <summary>A step whose Execute is unreachable: its timeout is refused before an attempt begins.</summary>
    private sealed class NeverRunsStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Never runs";
        public override string Description => "Its timeout is refused before Execute is reached.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("this step must never execute");

        public override Step<EmptyStepResultContext> Clone() => new NeverRunsStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class DependencyTimeoutStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Dependency timeout";
        public override string Description => "Throws a dependency's TimeoutException while its own deadline has time.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new TimeoutException("the dependency gave up");

        public override Step<EmptyStepResultContext> Clone() => new DependencyTimeoutStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class StepWithFailingCleanup : Step<EmptyStepResultContext>, IHasCleanupStep
    {
        public override string Name => "Owns a failing cleanup";
        public override string Description => "Succeeds, then its cleanup step fails.";
        public override bool DoesReturn => false;

        public StepGeneric? CreateCleanupStep(VariableStore variableStore) => new FailingStep();

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new StepWithFailingCleanup().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class StepWithFailingPreStep : Step<EmptyStepResultContext>, IHasPreStep
    {
        public override string Name => "Needs a pre-step";
        public override string Description => "Its pre-step fails, so the main stage should never run.";
        public override bool DoesReturn => false;

        public StepGeneric? CreatePreStep(VariableStore variableStore) => new FailingStep();

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new StepWithFailingPreStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

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

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class RecordingStep : Step<EmptyStepResultContext>
    {
        // Static on purpose: the run clones the authored step, so an instance flag would be set on the
        // clone and read from the original.
        public static bool Executed;

        public override string Name => "Records that it ran";
        public override string Description => "Sets a flag the test reads.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            Executed = true;
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new RecordingStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
