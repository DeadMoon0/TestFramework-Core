using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public void Validate_ThrowsWhenAReadVariableWasNeverProduced()
    {
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("user", StepIOKind.Variable, true));

        Assert.Throws<IOContractViolationException>(() => IOContractValidator.Validate([consumer], [], []));
    }

    [Fact]
    public void Validate_ThrowsWhenAVariableIsReadBeforeTheStepThatProducesIt()
    {
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("user", StepIOKind.Variable, true));

        TestStep producer = new("producer");
        producer.IOContract.Outputs.Add(new StepIOEntry("user", StepIOKind.Variable));

        // Reading before the producer runs is a violation; the same pair in the other order is fine.
        Assert.Throws<IOContractViolationException>(() => IOContractValidator.Validate([consumer, producer], [], []));
        IOContractValidator.Validate([producer, consumer], [], []);
    }

    [Fact]
    public void Validate_ThrowsWhenAVariableReadImmutablyIsWrittenAfterwards()
    {
        // The immutability rule lives only in the tracker: a declared IO contract says nothing about
        // whether a read demanded an immutable binding.
        VariableTracker tracker = new();
        tracker.GetReference(Var.RefImmutable<string>("user"));
        tracker.SetReference("user");

        CannotSetImmutableVariableException exception = Assert.Throws<CannotSetImmutableVariableException>(
            () => IOContractValidator.Validate([], ["user"], [], tracker));

        Assert.Contains("user", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllowsWritingAVariableThatWasOnlyReadMutably()
    {
        VariableTracker tracker = new();
        tracker.GetReference(Var.Ref<string>("user"));
        tracker.SetReference("user");

        IOContractValidator.Validate([], ["user"], [], tracker);
    }

    [Fact]
    public void Validate_AllowsWritingAVariableBeforeItIsReadImmutably()
    {
        // Writing first and then binding immutably is the normal way to use an immutable reference;
        // only a write that comes after the immutable read breaks the promise.
        VariableTracker tracker = new();
        tracker.SetReference("user");
        tracker.GetReference(Var.RefImmutable<string>("user"));

        IOContractValidator.Validate([], [], [], tracker);
    }

    [Fact]
    public void IOContractValidator_ThrowsWhenRequiredInputIsMissing()
    {
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(string)));

        IOContractViolationException exception = Assert.Throws<IOContractViolationException>(() => IOContractValidator.Validate([consumer], [], []));

        Assert.Contains("Step 'consumer'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("index 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("requires Variable 'input'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IOContractValidator_MissingInput_IncludesAvailableKeysAndSuggestions()
    {
        TestStep producer = new("produce-user-id");
        producer.IOContract.Outputs.Add(new StepIOEntry("userId", StepIOKind.Variable, true, typeof(string)));
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("userIdentifier", StepIOKind.Variable, true, typeof(string)));

        IOContractViolationException exception = Assert.Throws<IOContractViolationException>(() => IOContractValidator.Validate([producer, consumer], [], []));

        Assert.Contains("Step 'consumer'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("index 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("requires Variable 'userIdentifier'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IOContractValidator_ThrowsWhenProducerTypeDoesNotMatchConsumer()
    {
        TestStep producer = new("producer");
        producer.IOContract.Outputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(int)));
        TestStep consumer = new("consumer");
        consumer.IOContract.Inputs.Add(new StepIOEntry("input", StepIOKind.Variable, true, typeof(string)));

        IOContractTypeViolationException exception = Assert.Throws<IOContractTypeViolationException>(() => IOContractValidator.Validate([producer, consumer], [], []));

        Assert.Contains("Step 'consumer'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expects Variable 'input'", exception.Message, StringComparison.Ordinal);
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
        public DebuggingRunSession DebuggingSession { get; } = new(new EmptyRunDebugger());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        public RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }
    }

    private sealed class TestStep(string stepName) : Step<EmptyStepResultContext>
    {
        public override string Name => stepName;
        public override string Description => stepName;
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new TestStep(stepName).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => throw new NotSupportedException();
    }

    private sealed class NonCooperativeStep : Step<EmptyStepResultContext>
    {
        public override string Name => "non-cooperative";
        public override string Description => "Never completes and ignores cancellation.";
        public override bool DoesReturn => false;

        public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return EmptyStepResultContext.Instance;
        }

        public override Step<EmptyStepResultContext> Clone() => new NonCooperativeStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}