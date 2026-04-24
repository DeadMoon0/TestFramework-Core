using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps.Preprocessor;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class CoreAdvancedTests
{
    [Fact]
    public void VariableTracker_ThrowsDoesNotExist_WhenReferencedVariableWasNeverDefined()
    {
        VariableTracker tracker = new();
        tracker.GetReference(Var.Ref<string>("user"));

        Assert.Throws<VariableDoesNotExistException>(() => tracker.EnsureValidity([], CreateRuntime().VariableStore));
    }

    [Fact]
    public void VariableTracker_ThrowsDoesNotYetExist_WhenVariableIsReadBeforeItIsSet()
    {
        VariableTracker tracker = new();
        tracker.GetReference(Var.Ref<string>("user"));
        tracker.SetReference("user");

        Assert.Throws<VariableDoesNotYetExistException>(() => tracker.EnsureValidity([], CreateRuntime().VariableStore));
    }

    [Fact]
    public void VariableTracker_ThrowsWhenImmutableVariableIsLaterSet()
    {
        VariableTracker tracker = new();
        tracker.GetReference(Var.RefImmutable<string>("user"));
        tracker.SetReference("user");

        Assert.Throws<CannotSetImmutableVariableException>(() => tracker.EnsureValidity(["user"], CreateRuntime().VariableStore));
    }

    [Fact]
    public void IOContractValidator_ThrowsWhenRequiredInputIsMissing()
    {
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(string)));

        Assert.Throws<IOContractViolationException>(() => IOContractValidator.Validate([consumer], [], []));
    }

    [Fact]
    public void IOContractValidator_ThrowsWhenProducerTypeDoesNotMatchConsumer()
    {
        TestStep producer = new("producer");
        producer.IOContract.Outputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(int)));
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(string)));

        Assert.Throws<IOContractTypeViolationException>(() => IOContractValidator.Validate([producer, consumer], [], []));
    }

    [Fact]
    public void ConditionalStepEmitter_EmitsNestedStep_WhenConditionIsTrue()
    {
        RuntimeContext runtime = CreateRuntime();
        ConditionalStepEmitter emitter = new(Var.Const(true), builder => builder.Trigger(new TestStep("nested")));

        StepEmitterStepResult[] results = emitter.Emit(runtime.ArtifactStore, runtime.VariableStore, new VariableTracker(), new ArtifactTracker(), runtime.Logger).ToArray();

        StepEmitterStepResult result = Assert.Single(results);
        Assert.Equal("nested", result.Step.Name);
        Assert.False(result.RedirectToCleanUp);
        Assert.False(result.RunInPreSetupStage);
    }

    [Fact]
    public void ConditionalStepEmitter_SkipsNestedSteps_WhenConditionIsFalse()
    {
        RuntimeContext runtime = CreateRuntime();
        ConditionalStepEmitter emitter = new(Var.Const(false), builder => builder.Trigger(new TestStep("nested")));

        StepEmitterStepResult[] results = emitter.Emit(runtime.ArtifactStore, runtime.VariableStore, new VariableTracker(), new ArtifactTracker(), runtime.Logger).ToArray();

        Assert.Empty(results);
    }

    [Fact]
    public void ConditionalStepEmitter_RejectsModifiers()
    {
        RuntimeContext runtime = CreateRuntime();
        ConditionalStepEmitter emitter = new(Var.Const(true), builder => builder.Trigger(new TestStep("nested")));

        Assert.Throws<NotSupportedException>(() => emitter.Emit(
            runtime.ArtifactStore,
            runtime.VariableStore,
            new VariableTracker(),
            new ArtifactTracker(),
            [static (_, _, _) => { }],
            runtime.Logger).ToArray());
    }

    [Fact]
    public async Task TimelineRun_TimesOutStep_WhenStepIgnoresCancellation()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NonCooperativeStep())
            .Name("hang")
            .WithTimeOut(TimeSpan.FromMilliseconds(100))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.Step("hang").Should().HaveTimedOut();
    }

    private static RuntimeContext CreateRuntime() => new();

    private sealed class RuntimeContext
    {
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(EmptyRunDebugger.CreateNew());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        public RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }
    }

    private sealed class TestStep(string stepName) : Step<object?>
    {
        public override string Name => stepName;
        public override string Description => stepName;
        public override bool DoesReturn => false;

        public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult((object?)null);

        public override Step<object?> Clone() => new TestStep(stepName).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<object?>, object?> GetInstance() => throw new NotSupportedException();
    }

    private sealed class NonCooperativeStep : Step<object?>
    {
        public override string Name => "non-cooperative";
        public override string Description => "Never completes and ignores cancellation.";
        public override bool DoesReturn => false;

        public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return null;
        }

        public override Step<object?> Clone() => new NonCooperativeStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<object?>, object?> GetInstance() => new(this);
    }
}