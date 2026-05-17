using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
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
    [Fact]
    public async Task RunStage_RunsIndependentStepsInParallel()
    {
        TaskCompletionSource firstStepStarted = CreateSignal();
        TaskCompletionSource secondStepStarted = CreateSignal();
        TaskCompletionSource releaseSteps = CreateSignal();

        StageInstance stage = CreateStageInstance(
            new BlockingStep("first", firstStepStarted, releaseSteps),
            new BlockingStep("second", secondStepStarted, releaseSteps));

        RuntimeContext runtime = RuntimeContext.Create();
        CoreRunner runner = new();

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstStepStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await secondStepStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        releaseSteps.TrySetResult();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(StageState.Complete, stage.Result.State);
        Assert.All(stage.Steps, step => Assert.Equal(StepState.Complete, step.State));
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
        CoreRunner runner = new();

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await producerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await AssertDoesNotCompleteAsync(consumerStarted.Task, TimeSpan.FromMilliseconds(200));

        producerRelease.TrySetResult();
        await consumerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

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
        CoreRunner runner = new();

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await exclusiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await AssertDoesNotCompleteAsync(parallelCandidateStarted.Task, TimeSpan.FromMilliseconds(200));

        exclusiveRelease.TrySetResult();
        await parallelCandidateStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

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

        CoreRunner runner = new();

        Task runTask = runner.RunStage(stage, runtime.ServiceProvider, runtime.Logger, runtime.VariableStore, runtime.ArtifactStore, runtime.DebuggingSession);

        await firstSetupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await AssertDoesNotCompleteAsync(secondSetupStarted.Task, TimeSpan.FromMilliseconds(200));

        firstSetupRelease.TrySetResult();
        await secondSetupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(StageState.Complete, stage.Result.State);
        Assert.All(runtime.ArtifactStore.GetAll(), artifact => Assert.Equal(TestFramework.Core.Artifacts.ArtifactState.Setup, artifact.State));
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

    private sealed class BlockingStep : Step<EmptyStepResultContext>
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

        public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
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

        public override async Task Setup(IServiceProvider serviceProvider, TestSerializedArtifactData data, TestSerializedArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public override Task Deconstruct(IServiceProvider serviceProvider, TestSerializedArtifactReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;

        public override string ToString() => "serialized-artifact";
    }

    private sealed class TestSerializedArtifactData : ArtifactData<TestSerializedArtifactData, TestSerializedArtifactDescriber, TestSerializedArtifactReference>
    {
        public override string ToString() => "artifact-data";
    }

    private sealed class TestSerializedArtifactReference(string name, TestSerializedArtifactData data) : ArtifactReference<TestSerializedArtifactReference, TestSerializedArtifactDescriber, TestSerializedArtifactData>
    {
        public override Task<ArtifactResolveResult<TestSerializedArtifactDescriber, TestSerializedArtifactData, TestSerializedArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
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

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => new TestSerializedArtifactDescriber();

        public override string ToString() => name;
    }
}