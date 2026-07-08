using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;
using Xunit.Abstractions;
using System.Linq;

namespace TestFramework.Core.Tests;

public class CoreTimelineCoverageTests
{
    [Fact]
    public async Task TimelineRun_SetVariableAndAssertVariable_PersistsPublicRunState()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("user", Var.Const("Ada"))
            .Name("set-user")
            .AssertVariable(Var.Ref<string>("user"), value => value == "Ada")
            .Name("assert-user")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(Steps.StepState.Complete, run.Step("set-user").State);
        Assert.Equal(Steps.StepState.Complete, run.Step("assert-user").State);
        run.Variable<string>("user").Should().Exist().And().Be("Ada");
    }

    [Fact]
    public async Task TimelineRun_EnsureRanToCompletion_ThrowsWhenAssertionFails()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("user", Var.Const("Ada"))
            .Name("set-user")
            .AssertVariable(Var.Ref<string>("user"), value => value == "Grace")
            .Name("assert-user")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains(exception.FailedSteps, step => step.StepException is AssertVariableException);
    }

    [Fact]
    public async Task TimelineRun_AddArtifact_ExposesArtifactThroughPublicHandle()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("artifact", new TestArtifactReference("artifact-ref"), new TestArtifactData("payload"))
            .RunAsync();

        run.EnsureRanToCompletion();
        run.Artifact<TestArtifactData>("artifact").Select(data => data.Value).Should().Be("payload");
    }

    [Fact]
    public async Task TimelineRun_WithRetry_RetriesUntilStepSucceeds()
    {
        RetryProbe probe = new();
        Timeline timeline = Timeline.Create()
            .Trigger(new FlakyStep(probe, failuresBeforeSuccess: 2))
            .Name("flaky")
            .WithRetry(2, CalcDelays.None)
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(3, probe.Attempts);
        Assert.Equal(3, run.Step("flaky").RetryResults.Count);
    }

    [Fact]
    public async Task TimelineRun_GetValue_PersistsExplicitStepOutputVariable()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new ReturnConstantStep("logic-run"))
            .Name("return-run")
            .GetValue("logicRun")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        run.Variable<string>("logicRun").Should().Exist().And().Be("logic-run");
    }

    [Fact]
    public async Task TimelineRun_GetValue_DeclaresTypedOutputVariable()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new ReturnConstantStep("logic-run"))
            .Name("return-run")
            .GetValue("logicRun")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        run.Variable<string>("logicRun").Should().Exist().And().Be("logic-run");
        Assert.Single(run.Step("return-run").Step.IOContract.Outputs.Where(entry => entry.Key == "logicRun" && entry.Kind == StepIOKind.Variable && entry.DeclaredType == typeof(string)));
    }

    [Fact]
    public async Task TimelineRun_WaitForSequentialReturningEvent_DoesNotCompleteBeforePayloadIsAvailable()
    {
        ReturningSequentialProbeEvent probeEvent = new();
        Timeline timeline = Timeline.Create()
            .WaitForEvent(probeEvent)
            .Name("await-event")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(2, probeEvent.Attempts);
        Assert.Equal("done", Assert.IsType<TextResultContext>(run.Step("await-event").LastResult.Result).Value);
    }

    [Fact]
    public async Task Timeline_BuildOnce_CanCreateIndependentRuns_WithSetupRunAtMethodScope()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Ada"))
            .Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
            .Build();

        TimelineRun firstRun = await timeline.SetupRun().RunAsync();
        TimelineRun secondRun = await timeline.SetupRun().RunAsync();

        firstRun.EnsureRanToCompletion();
        secondRun.EnsureRanToCompletion();
        firstRun.Variable<string>("greeting").Should().Exist().And().Be("Hello Ada");
        secondRun.Variable<string>("greeting").Should().Exist().And().Be("Hello Ada");
    }

    [Fact]
    public async Task TimelineRun_ForEach_ExpandsOneLabeledStepPerItem()
    {
        List<string> seen = [];
        Timeline timeline = Timeline.Create()
            .ForEach(new[] { "Ada", "Grace" }, "item", loop =>
            {
                loop.Trigger(new CaptureVariableStep("item", seen)).Name("capture");
            })
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(new[] { "Ada", "Grace" }, seen);
        Assert.Equal(2, run.Steps("capture").Count);
        Assert.Throws<StepLabelAmbiguousException>(() => run.Step("capture"));
    }

    [Fact]
    public async Task TimelineRun_SetupRunWithOutputHelper_WritesCapturedLogLines()
    {
        RecordingOutputHelper output = new();
        Timeline timeline = Timeline.Create()
            .Trigger(new LoggingStep("hello from step"))
            .Name("log")
            .Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains(output.Lines, line => line.Contains("hello from step", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimelineRun_SetupRunWithOutputHelper_WritesExecutionDiagnostics()
    {
        RecordingOutputHelper output = new();
        Timeline timeline = Timeline.Create()
            .Trigger(new ReturnConstantStep("logic-run"))
            .Name("return-run")
            .GetValue("logicRun")
            .Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains(output.Lines, line => line.Contains("Timeline Run", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("Stage Plan", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("Executing Step: Return Constant", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("Bindings: logicRun", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("Stage Summary:", StringComparison.Ordinal));
        Assert.DoesNotContain(output.Lines, line => line.Contains("<blank>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimelineRun_SetupRunWithOutputHelper_WritesRetryDiagnostics()
    {
        RecordingOutputHelper output = new();
        RetryProbe probe = new();
        Timeline timeline = Timeline.Create()
            .Trigger(new FlakyStep(probe, failuresBeforeSuccess: 1))
            .Name("flaky")
            .WithRetry(2, CalcDelays.None)
            .Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains(output.Lines, line => line.Contains("retry scheduled", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("Retry Attempt 2:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimelineRun_AssertionScope_GroupsFailuresUntilDispose()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        MultipleAssertionsFailedException exception = Assert.Throws<MultipleAssertionsFailedException>(() =>
        {
            using AssertionScope scope = run.AssertionScope();
            run.Assert(1).Should().Be(2);
            run.Assert("alpha").Should().Be("beta");
        });

        Assert.Equal(2, exception.Failures.Count);
    }

    [Fact]
    public void EmptyServiceProvider_GetService_ReturnsNull()
    {
        EmptyServiceProvider provider = new();

        object? service = provider.GetService(typeof(string));

        Assert.Null(service);
    }

    [Fact]
    public void FreezableCollectionsAndDictionaries_SupportCommonMutableOperationsBeforeFreeze()
    {
        FreezableCollection<string> collection = new();
        collection.Add("alpha");
        collection.Add("beta");

        Assert.Contains("alpha", collection);
        string[] copied = new string[2];
        collection.CopyTo(copied, 0);
        Assert.Equal(new[] { "alpha", "beta" }, copied);
        Assert.True(collection.Remove("beta"));
        Assert.Single(collection);

        FreezableDictionary<string, int> dictionary = new();
        dictionary.Add("one", 1);
        dictionary["two"] = 2;

        Assert.True(dictionary.ContainsKey("one"));
        Assert.Contains(new KeyValuePair<string, int>("one", 1), dictionary);
        KeyValuePair<string, int>[] target = new KeyValuePair<string, int>[2];
        dictionary.CopyTo(target, 0);
        Assert.Equal(2, target.Length);
        Assert.True(dictionary.Remove("two"));
        Assert.True(dictionary.Remove(new KeyValuePair<string, int>("one", 1)));
        Assert.Empty(dictionary);
    }

    private sealed class TestArtifactReference : ArtifactReference<TestArtifactReference, TestArtifactDescriber, TestArtifactData>
    {
        private readonly string _value;

        public TestArtifactReference(string value)
        {
            _value = value;
        }

        public override Task<ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, Logging.ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
            {
                Found = true,
                Data = new TestArtifactData(_value) { Identifier = versionIdentifier }
            });

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => _value;
    }

    private sealed class TestArtifactData : ArtifactData<TestArtifactData, TestArtifactDescriber, TestArtifactReference>
    {
        public TestArtifactData(string value)
        {
            Value = value;
        }

        public string Value { get; }
        public override string ToString() => Value;
    }

    private sealed class TestArtifactDescriber : ArtifactDescriber<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, TestArtifactData data, TestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger) => Task.CompletedTask;
        public override Task Deconstruct(IServiceProvider serviceProvider, TestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger) => Task.CompletedTask;
        public override string ToString() => "TestArtifact";
    }

    private sealed class RetryProbe
    {
        public int Attempts { get; private set; }

        public int Increment() => ++Attempts;
    }

    private sealed class FlakyStep(RetryProbe probe, int failuresBeforeSuccess) : Step<TextResultContext>
    {
        public override string Name => "Flaky Step";
        public override string Description => "Fails a fixed number of times before succeeding.";
        public override bool DoesReturn => true;

        public override Task<TextResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            int attempt = probe.Increment();
            if (attempt <= failuresBeforeSuccess)
                throw new InvalidOperationException($"failure {attempt}");
            return Task.FromResult<TextResultContext?>(new TextResultContext("ok"));
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Step<TextResultContext> Clone() => new FlakyStep(probe, failuresBeforeSuccess).WithClonedOptions(this);

        public override StepInstance<Step<TextResultContext>, TextResultContext> GetInstance() => new(this);
    }

    private sealed class CaptureVariableStep(string variableName, List<string> seen) : Step<EmptyStepResultContext>
    {
        public override string Name => "Capture Variable Step";
        public override string Description => "Captures the current loop variable value.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            seen.Add(variableStore.GetVariable<string>(variableName)!);
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override void DeclareIO(StepIOContract contract)
        {
            contract.Inputs.Add(new StepIOEntry(variableName, StepIOKind.Variable, true, typeof(string)));
        }

        public override Step<EmptyStepResultContext> Clone() => new CaptureVariableStep(variableName, seen).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class LoggingStep(string message) : Step<EmptyStepResultContext>
    {
        public override string Name => "Logging Step";
        public override string Description => "Writes a log line through the scoped logger.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation(message);
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Step<EmptyStepResultContext> Clone() => new LoggingStep(message).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class ReturnConstantStep(string value) : Step<TextResultContext>
    {
        public override string Name => "Return Constant";
        public override string Description => "Returns a constant string.";
        public override bool DoesReturn => true;

        public override Task<TextResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<TextResultContext?>(new TextResultContext(value));

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Step<TextResultContext> Clone() => new ReturnConstantStep(value).WithClonedOptions(this);

        public override StepInstance<Step<TextResultContext>, TextResultContext> GetInstance() => new(this);
    }

    private sealed class ReturningSequentialProbeEvent : SequentialEvent<ReturningSequentialProbeEvent, TextResultContext>
    {
        public int Attempts { get; private set; }

        public override string Name => "ReturningSequentialProbeEvent";

        public override string Description => "Returns an incomplete poll once before yielding a value.";

        public override bool DoesReturn => true;

        public override Step<TextResultContext> Clone() => this;

        public override Task<SequentialPollingResult<TextResultContext>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(Attempts == 1
            ? new SequentialPollingResult<TextResultContext>(false, null, TimeSpan.Zero)
            : new SequentialPollingResult<TextResultContext>(true, new TextResultContext("done"), TimeSpan.Zero));
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }
    }

    private sealed class RecordingOutputHelper : ITestOutputHelper
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message)
        {
            Lines.Add(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            Lines.Add(string.Format(format, args));
        }
    }

}

internal sealed record TextResultContext(string Value) : StepResultContext;

internal static class CoreTimelineCoverageTestExtensions
{
    public static ITimelineBuilderModifier<TextResultContext> GetValue(this ITimelineBuilderModifier<TextResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Value, identifier);
}