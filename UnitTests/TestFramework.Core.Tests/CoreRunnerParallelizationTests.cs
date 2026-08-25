using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Logging;
using TestFramework.Core.Runner;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps.SystemSteps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class CoreRunnerParallelizationTests
{
    /// <summary>
    /// Budget for "this signal should eventually arrive" waits.
    /// </summary>
    /// <remarks>
    /// Deliberately generous. These tests assert ordering and barrier semantics, not latency, so a
    /// tight budget only converts CPU contention - a two-core runner executing collections in
    /// parallel - into a false failure. The negative assertions keep their short windows, because
    /// a slow machine can only make "this must not have started yet" more likely to hold.
    /// </remarks>
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);
    [Fact]
    public async Task RunStage_RunsIndependentPrepareStepsInParallel()
    {
        TaskCompletionSource firstStepStarted = CreateSignal();
        TaskCompletionSource secondStepStarted = CreateSignal();
        TaskCompletionSource releaseSteps = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new PrepareBlockingStep("first", firstStepStarted, releaseSteps),
            new PrepareBlockingStep("second", secondStepStarted, releaseSteps));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstStepStarted.Task.WaitAsync(SignalTimeout);
        await secondStepStarted.Task.WaitAsync(SignalTimeout);

        releaseSteps.TrySetResult();
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
        Assert.All(stage.Steps, step => Assert.Equal(StepState.Complete, step.State));
    }

    [Fact]
    public async Task RunStage_DoesNotMergeIndependentActStepsByDefault()
    {
        TaskCompletionSource firstStepStarted = CreateSignal();
        TaskCompletionSource firstStepRelease = CreateSignal();
        TaskCompletionSource secondStepStarted = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new BlockingStep("first", firstStepStarted, firstStepRelease),
            new BlockingStep("second", secondStepStarted, CreateSignal(completed: true)));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstStepStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(secondStepStarted.Task, TimeSpan.FromMilliseconds(200));

        firstStepRelease.TrySetResult();
        await secondStepStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    [Fact]
    public async Task RunStage_PreservesInterleavedPhaseBoundaries()
    {
        TaskCompletionSource firstPrepareStarted = CreateSignal();
        TaskCompletionSource firstPrepareRelease = CreateSignal();
        TaskCompletionSource actStarted = CreateSignal();
        TaskCompletionSource actRelease = CreateSignal();
        TaskCompletionSource secondPrepareStarted = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new PrepareBlockingStep("prepare-1", firstPrepareStarted, firstPrepareRelease),
            new BlockingStep("act", actStarted, actRelease),
            new PrepareBlockingStep("prepare-2", secondPrepareStarted, CreateSignal(completed: true)));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstPrepareStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(actStarted.Task, TimeSpan.FromMilliseconds(200));
        await AssertDoesNotCompleteAsync(secondPrepareStarted.Task, TimeSpan.FromMilliseconds(200));

        firstPrepareRelease.TrySetResult();
        await actStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(secondPrepareStarted.Task, TimeSpan.FromMilliseconds(200));

        actRelease.TrySetResult();
        await secondPrepareStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    [Fact]
    public async Task RunStage_WaitsForDeclaredIoDependenciesBeforeStartingConsumer()
    {
        TaskCompletionSource producerStarted = CreateSignal();
        TaskCompletionSource producerRelease = CreateSignal();
        TaskCompletionSource consumerStarted = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new BlockingStep("producer", producerStarted, producerRelease, outputs: [new StepIOEntry("user", StepIOKind.Variable)]),
            new BlockingStep("consumer", consumerStarted, CreateSignal(completed: true), inputs: [new StepIOEntry("user", StepIOKind.Variable)]));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await producerStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(consumerStarted.Task, TimeSpan.FromMilliseconds(200));

        producerRelease.TrySetResult();
        await consumerStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    [Fact]
    public async Task RunStage_RespectsExclusiveStepsAsBarriers()
    {
        TaskCompletionSource exclusiveStarted = CreateSignal();
        TaskCompletionSource exclusiveRelease = CreateSignal();
        TaskCompletionSource parallelCandidateStarted = CreateSignal();

        var exclusiveStep = new BlockingStep("exclusive", exclusiveStarted, exclusiveRelease);
        exclusiveStep.ExecutionOptions.ParallelizationMode = StepParallelizationMode.DoNotParallelize;

        StageInstance stage = CreateStageInstance(
            exclusiveStep,
            new BlockingStep("candidate", parallelCandidateStarted, CreateSignal(completed: true)));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await exclusiveStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(parallelCandidateStarted.Task, TimeSpan.FromMilliseconds(200));

        exclusiveRelease.TrySetResult();
        await parallelCandidateStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    [Fact]
    public async Task RunStage_SerializesSetupArtifactStepsByArtifactTypeWhenConfigured()
    {
        TaskCompletionSource firstSetupStarted = CreateSignal();
        TaskCompletionSource firstSetupRelease = CreateSignal();
        TaskCompletionSource secondSetupStarted = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new SetupArtifactStep(new ArtifactIdentifier("artifact-a")),
            new SetupArtifactStep(new ArtifactIdentifier("artifact-b")));

        RuntimeContext runtime = RuntimeContext.Create();
        runtime.ArtifactStore.AddArtifact(new ArtifactInstance<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>(
            new TestSerializedArtifactDescriber(firstSetupStarted, firstSetupRelease),
            new ArtifactIdentifier("artifact-a"),
            new TestSerializedArtifactReference("a", new TestSerializedArtifactData()),
            new TestSerializedArtifactData()));
        runtime.ArtifactStore.AddArtifact(new ArtifactInstance<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>(
            new TestSerializedArtifactDescriber(secondSetupStarted, CreateSignal(completed: true)),
            new ArtifactIdentifier("artifact-b"),
            new TestSerializedArtifactReference("b", new TestSerializedArtifactData()),
            new TestSerializedArtifactData()));

        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstSetupStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(secondSetupStarted.Task, TimeSpan.FromMilliseconds(200));

        firstSetupRelease.TrySetResult();
        await secondSetupStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
        Assert.All(runtime.ArtifactStore.GetAll(), artifact => Assert.Equal(TestFramework.Core.Artifacts.ArtifactState.Setup, artifact.State));
    }

    [Fact]
    public async Task RunStage_AllowsSetupArtifactStepsToRunInParallelWhenCustomResourceKeysDiffer()
    {
        TaskCompletionSource firstSetupStarted = CreateSignal();
        TaskCompletionSource secondSetupStarted = CreateSignal();
        TaskCompletionSource releaseSetups = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new SetupArtifactStep(new ArtifactIdentifier("artifact-a")),
            new SetupArtifactStep(new ArtifactIdentifier("artifact-b")));

        RuntimeContext runtime = RuntimeContext.Create();
        runtime.ArtifactStore.AddArtifact(new ArtifactInstance<TestKeyedArtifactDescriber, TestKeyedArtifactData, TestKeyedArtifactReference>(
            new TestKeyedArtifactDescriber(firstSetupStarted, releaseSetups),
            new ArtifactIdentifier("artifact-a"),
            new TestKeyedArtifactReference("a", "sql-a"),
            new TestKeyedArtifactData()));
        runtime.ArtifactStore.AddArtifact(new ArtifactInstance<TestKeyedArtifactDescriber, TestKeyedArtifactData, TestKeyedArtifactReference>(
            new TestKeyedArtifactDescriber(secondSetupStarted, releaseSetups),
            new ArtifactIdentifier("artifact-b"),
            new TestKeyedArtifactReference("b", "sql-b"),
            new TestKeyedArtifactData()));

        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstSetupStarted.Task.WaitAsync(SignalTimeout);
        await secondSetupStarted.Task.WaitAsync(SignalTimeout);

        releaseSetups.TrySetResult();
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    [Fact]
    public async Task RunStage_WaitsForArtifactSetupBeforeStartingSameArtifactConsumer()
    {
        TaskCompletionSource setupStarted = CreateSignal();
        TaskCompletionSource setupRelease = CreateSignal();
        TaskCompletionSource consumerStarted = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new SetupArtifactStep(new ArtifactIdentifier("artifact-a")),
            new BlockingStep(
                "consumer",
                consumerStarted,
                CreateSignal(completed: true),
                inputs: [new StepIOEntry("artifact-a", StepIOKind.Artifact)]));

        RuntimeContext runtime = RuntimeContext.Create();
        runtime.ArtifactStore.AddArtifact(new ArtifactInstance<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>(
            new TestSerializedArtifactDescriber(setupStarted, setupRelease),
            new ArtifactIdentifier("artifact-a"),
            new TestSerializedArtifactReference("a", new TestSerializedArtifactData()),
            new TestSerializedArtifactData()));

        CoreRunner runner = new(StepObservers.None, ValueResolution.Empty);

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await setupStarted.Task.WaitAsync(SignalTimeout);
        await AssertDoesNotCompleteAsync(consumerStarted.Task, TimeSpan.FromMilliseconds(200));

        setupRelease.TrySetResult();
        await consumerStarted.Task.WaitAsync(SignalTimeout);
        await runTask.WaitAsync(SignalTimeout);

        Assert.Equal(StageState.Complete, stage.Result.State);
    }

    private static async Task AssertDoesNotCompleteAsync(Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.NotSame(task, completed);
    }

    private static TaskCompletionSource CreateSignal(bool completed = false)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (completed)
            signal.TrySetResult();
        return signal;
    }

    private static StageInstance CreateStageInstance(params StepGeneric[] steps)
    {
        Stage stage = new()
        {
            Name = "Parallel Stage",
            Description = "Stage used by runner parallelization tests."
        };

        foreach (var step in steps)
        {
            step.DeclareIO(step.IOContract);
            stage.Steps.Add(step);
        }

        return new StageInstance(stage);
    }

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(new EmptyRunDebugger());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        private RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }

        public static RuntimeContext Create() => new();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class BlockingStep : Step<EmptyStepResultContext>
    {
        private readonly string name;
        private readonly TaskCompletionSource started;
        private readonly TaskCompletionSource release;
        private readonly IReadOnlyList<StepIOEntry> inputs;
        private readonly IReadOnlyList<StepIOEntry> outputs;

        public BlockingStep(string name, TaskCompletionSource started, TaskCompletionSource release, IReadOnlyList<StepIOEntry>? inputs = null, IReadOnlyList<StepIOEntry>? outputs = null)
        {
            this.name = name;
            this.started = started;
            this.release = release;
            this.inputs = inputs ?? [];
            this.outputs = outputs ?? [];
        }

        public override string Name => name;
        public override string Description => $"Blocking step '{name}'.";
        public override bool DoesReturn => false;

        public override async Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(2), context.Deadline.Token);
            return EmptyStepResultContext.Instance;
        }

        public override void DeclareIO(StepIOContract contract)
        {
            foreach (var input in inputs)
                contract.Inputs.Add(input);

            foreach (var output in outputs)
                contract.Outputs.Add(output);
        }

        public override Step<EmptyStepResultContext> Clone() => new BlockingStep(name, started, release, inputs, outputs).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class PrepareBlockingStep : BlockingStep
    {
        private readonly string stepName;
        private readonly TaskCompletionSource started;
        private readonly TaskCompletionSource release;
        private readonly IReadOnlyList<StepIOEntry>? inputs;
        private readonly IReadOnlyList<StepIOEntry>? outputs;

        public PrepareBlockingStep(string name, TaskCompletionSource started, TaskCompletionSource release, IReadOnlyList<StepIOEntry>? inputs = null, IReadOnlyList<StepIOEntry>? outputs = null)
            : base(name, started, release, inputs, outputs)
        {
            stepName = name;
            this.started = started;
            this.release = release;
            this.inputs = inputs;
            this.outputs = outputs;
        }

        public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

        public override Step<EmptyStepResultContext> Clone() => new PrepareBlockingStep(stepName, started, release, inputs, outputs).WithClonedOptions(this);
    }

    private sealed class TestSerializedArtifactDescriber : ArtifactDescriber<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>
    {
        private readonly TaskCompletionSource started;
        private readonly TaskCompletionSource release;

        public TestSerializedArtifactDescriber() : this(CreateSignal(completed: true), CreateSignal(completed: true))
        {
        }

        public TestSerializedArtifactDescriber(TaskCompletionSource started, TaskCompletionSource release)
        {
            this.started = started;
            this.release = release;
        }

        public override ArtifactSetupParallelizationMode SetupParallelization => ArtifactSetupParallelizationMode.SerializeByArtifactType;

        public override async Task Setup(RunContext context, TestSerializedArtifactData data, TestSerializedArtifactReference reference)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(SignalTimeout);
        }

        public override Task Deconstruct(RunContext context, TestSerializedArtifactReference reference) => Task.CompletedTask;

        public override string ToString() => "serialized-artifact";
    }

    private sealed class TestSerializedArtifactData : ArtifactData<TestSerializedArtifactData, TestSerializedArtifactDescriber, TestSerializedArtifactReference>
    {
        public override string ToString() => "artifact-data";
    }

    private sealed class TestKeyedArtifactDescriber : ArtifactDescriber<TestKeyedArtifactDescriber, TestKeyedArtifactData, TestKeyedArtifactReference>
    {
        private readonly TaskCompletionSource started;
        private readonly TaskCompletionSource release;

        public TestKeyedArtifactDescriber() : this(CreateSignal(completed: true), CreateSignal(completed: true))
        {
        }

        public TestKeyedArtifactDescriber(TaskCompletionSource started, TaskCompletionSource release)
        {
            this.started = started;
            this.release = release;
        }

        public override ArtifactSetupParallelizationMode SetupParallelization => ArtifactSetupParallelizationMode.SerializeByArtifactType;

        public override string? GetSetupParallelizationResourceKey(ArtifactInstanceGeneric artifactInstance)
        {
            TestKeyedArtifactReference reference = (TestKeyedArtifactReference)artifactInstance.Reference;
            return $"keyed:{reference.ResourceKey}";
        }

        public override async Task Setup(RunContext context, TestKeyedArtifactData data, TestKeyedArtifactReference reference)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(SignalTimeout);
        }

        public override Task Deconstruct(RunContext context, TestKeyedArtifactReference reference) => Task.CompletedTask;

        public override string ToString() => "keyed-artifact";
    }

    private sealed class TestKeyedArtifactData : ArtifactData<TestKeyedArtifactData, TestKeyedArtifactDescriber, TestKeyedArtifactReference>
    {
        public override string ToString() => "keyed-artifact-data";
    }

    private sealed class TestKeyedArtifactReference(string name, string resourceKey) : ArtifactReference<TestKeyedArtifactReference, TestKeyedArtifactDescriber, TestKeyedArtifactData>
    {
        public string Name { get; } = name;

        public string ResourceKey { get; } = resourceKey;

        public override Task<ArtifactResolveResult<TestKeyedArtifactDescriber, TestKeyedArtifactData, TestKeyedArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
        {
            return Task.FromResult(new ArtifactResolveResult<TestKeyedArtifactDescriber, TestKeyedArtifactData, TestKeyedArtifactReference>
            {
                Found = true,
                Data = new TestKeyedArtifactData { Identifier = versionIdentifier }
            });
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
        }

        public override string ToString() => Name;
    }

    private sealed class TestSerializedArtifactReference(string name, TestSerializedArtifactData data) : ArtifactReference<TestSerializedArtifactReference, TestSerializedArtifactDescriber, TestSerializedArtifactData>
    {
        public override Task<ArtifactResolveResult<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
        {
            return Task.FromResult(new ArtifactResolveResult<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>
            {
                Found = true,
                Data = data
            });
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
        }

        public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => new TestSerializedArtifactDescriber();

        public override string ToString() => name;
    }
}