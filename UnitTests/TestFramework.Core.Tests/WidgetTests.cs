using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers a run recording the evidence it produced.
/// </summary>
/// <remarks>
/// A run says what happened but not what it looked like, so a browser test that walked four pages and a
/// queue test that moved three messages both come out as a row of named boxes. What is tested here is
/// the half that fixes that: a file in the run's own output, described, and attributed to the step and
/// attempt that produced it.
/// </remarks>
public sealed class WidgetTests : IDisposable
{
    /// <summary>A one-pixel PNG, so an image widget is a real one rather than bytes pretending.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string root = Path.Combine(Path.GetTempPath(), "tf-widgets-" + Guid.NewGuid().ToString("N"));
    private readonly string? previousOutput = System.Environment.GetEnvironmentVariable(RunOutput.DirectoryVariable);

    public WidgetTests()
        => System.Environment.SetEnvironmentVariable(RunOutput.DirectoryVariable, root);

    [Fact]
    public async Task AnWidgetIsFiledAgainstTheStepAndAttemptThatProducedIt()
    {
        // The reason widgets exist as their own signal. A value update carries the stage and step,
        // which is all a value needs; a step that retried produced one picture per attempt, and the
        // interesting one is rarely the last.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create()
            .Trigger(new CapturingStep(failUntilAttempt: 2))
                .WithRetry(1, CalcDelays.None).Name("checkout")
            .Build());

        Assert.Equal(2, debugger.Widgets.Count);

        Assert.All(debugger.Widgets, widget =>
        {
            Assert.Equal(WidgetKinds.Screenshot, widget.Kind);
            Assert.False(string.IsNullOrWhiteSpace(widget.Stage));
            Assert.NotNull(widget.StepId);
        });

        // One per attempt, and each says which it was.
        Assert.Equal([1, 2], debugger.Widgets.Select(widget => widget.Attempt).ToArray());
    }

    [Fact]
    public async Task AnWidgetIsWrittenIntoTheRunsOwnOutputBesideItsValues()
    {
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create().Trigger(new CapturingStep()).Name("shot").Build());

        DebugValueBody body = Assert.Single(debugger.Widgets).Description.Body!;

        // Named for what it is, so the shell opens it in an image viewer rather than asking which
        // program to try.
        Assert.Equal("widgets/page.png", body.RelativePath);
        Assert.Equal(Png, File.ReadAllBytes(body.Path));
    }

    [Fact]
    public async Task AnImageSaysItIsCutRatherThanTravellingInThePreview()
    {
        // What travels is nothing at all: a consumer offering to open the file is the only honest
        // thing to do with a picture, and hex of a PNG is what the alternative renders.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create().Trigger(new CapturingStep()).Name("shot").Build());

        DebugValueDescription description = Assert.Single(debugger.Widgets).Description;

        Assert.Equal(DebugPreviewForm.Image, description.Preview!.Form);
        Assert.Empty(description.Preview.Text);
        Assert.True(description.Preview.IsTruncated);
        Assert.NotNull(description.Body);
    }

    [Fact]
    public async Task ATextWidgetCarriesEnoughToRecogniseItBy()
    {
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create().Trigger(new DocumentStep()).Name("markup").Build());

        DebugWidgetEntry entry = Assert.Single(debugger.Widgets);

        Assert.Equal(WidgetKinds.Document, entry.Kind);
        Assert.Contains("<html>", entry.Description.Preview!.Text, StringComparison.Ordinal);
        Assert.Equal("widgets/page-source.html", entry.Description.Body!.RelativePath);
    }

    [Fact]
    public async Task TheSameEvidenceTwiceIsWrittenOnce()
    {
        // A step that takes the same screenshot on both attempts should not leave two identical
        // files, for the same reason an artifact republished unchanged does not.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create()
            .Trigger(new CapturingStep(failUntilAttempt: 2))
                .WithRetry(1, CalcDelays.None).Name("checkout")
            .Build());

        string[] paths = [.. debugger.Widgets.Select(widget => widget.Description.Body!.Path).Distinct()];

        Assert.Single(paths);
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(paths[0])!));
    }

    [Fact]
    public async Task AnObserverGatheringEvidenceIsFiledAgainstTheAttemptThatFailed()
    {
        // The path the browser pack takes: a step fails, and an observer photographs what it was
        // looking at. The observer never says which attempt it is — it runs inside the one that just
        // failed, and the run reads that for itself. If the scope did not reach here the evidence
        // would arrive attributed to nothing, and nothing would say so.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(
            debugger,
            Timeline.Create()
                .Trigger(new FailingStep())
                    .WithRetry(1, CalcDelays.None).Name("checkout")
                .Build(),
            new WatchingObserver());

        Assert.Equal([1, 2], debugger.Widgets.Select(widget => widget.Attempt).ToArray());
        Assert.All(debugger.Widgets, widget => Assert.False(string.IsNullOrWhiteSpace(widget.Stage)));
    }

    [Fact]
    public async Task AComponentNamesItselfBecauseTheRunCannot()
    {
        // Every environment component is built during one step, so without this their evidence is
        // indistinguishable — four widgets filed against the same step with nothing to tell them apart.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create().Trigger(new ComponentLogStep()).Name("logs").Build());

        Assert.Equal("api-container", Assert.Single(debugger.Widgets).Component);
    }

    [Fact]
    public async Task EvidenceIsKeptEvenWhenNothingIsWatching()
    {
        // It lives in the run's output, where a person can open it and a build publishes it. A run
        // whose screenshots existed only while a tool happened to be attached would be useless on CI.
        using JournalScope journal = JournalScope.Disarmed();

        await Timeline.Create().Trigger(new CapturingStep()).Name("shot").Build().SetupRun().RunAsync();

        string[] written = [.. Directory.EnumerateFiles(root, "page.png", SearchOption.AllDirectories)];

        Assert.Single(written);
        Assert.Equal(Png, File.ReadAllBytes(written[0]));
    }

    [Fact]
    public async Task AnWidgetSurvivesTheWireAsItWasProduced()
    {
        // The journal and the pipe carry the same envelopes, so what round-trips here is what a
        // recorded run hands back weeks later.
        WidgetRecordingDebugger debugger = new();

        await RunAsync(debugger, Timeline.Create().Trigger(new CapturingStep()).Name("shot").Build());

        DebugWidgetEntry produced = Assert.Single(debugger.Widgets);

        DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(
            new PipeWidgetSignal { SessionId = "s1", Entry = produced },
            1);

        PipeWidgetSignal read = Assert.IsType<PipeWidgetSignal>(
            DebugEnvelopeCodec.Unwrap(DebugEnvelopeCodec.Deserialize(DebugEnvelopeCodec.Serialize(envelope))));

        Assert.Equal(produced.Kind, read.Entry.Kind);
        Assert.Equal(produced.Name, read.Entry.Name);
        Assert.Equal(produced.Stage, read.Entry.Stage);
        Assert.Equal(produced.StepId, read.Entry.StepId);
        Assert.Equal(produced.Attempt, read.Entry.Attempt);
        Assert.Equal(produced.Description.Body!.ContentHash, read.Entry.Description.Body!.ContentHash);
        Assert.Equal(DebugPreviewForm.Image, read.Entry.Description.Preview!.Form);
    }

    [Fact]
    public async Task ADebuggerThatCannotCarryEvidenceIsNotAskedTo()
    {
        // A debugger built against a Core that had never heard of widgets keeps working, and simply
        // does not receive them. Widening the interface every debugger implements would have broken it.
        WidgetRecordingDebugger carries = new();
        OldFashionedDebugger cannot = new();

        await RunAsync(
            CompositeRunDebugger.Create(carries, cannot),
            Timeline.Create().Trigger(new CapturingStep()).Name("shot").Build());

        Assert.Single(carries.Widgets);
        Assert.True(cannot.SawSignals, "the older debugger should still have received everything it does understand");
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(RunOutput.DirectoryVariable, previousOutput);

        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that outlives the test is not a test failure.
        }
    }

    private static async Task RunAsync(IRunDebugger debugger, Timeline timeline, IStepObserver? observer = null)
    {
        using JournalScope journal = JournalScope.Disarmed();

        try
        {
            await timeline.SetupRun(new DebuggerServiceProvider(debugger, observer)).RunAsync();
        }
        catch (Exception)
        {
            // Some of these runs are meant to fail on their first attempt. Whether the failure also
            // reaches the caller is Core's business and not what is under test.
        }
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger, IStepObserver? observer = null) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRunDebugger))
                return debugger;

            return serviceType == typeof(IStepObserver) ? observer : null;
        }
    }

    /// <summary>Photographs whatever failed, the way the browser pack's observer does.</summary>
    private sealed class WatchingObserver : IStepObserver
    {
        public Task OnStepStartingAsync(StepObservation observation, RunContext run) => Task.CompletedTask;

        public Task OnStepFailedAsync(StepObservation observation, Exception exception, RunContext run)
        {
            run.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Screenshot,
                Name = "on-failure",
                Form = DebugPreviewForm.Image,

                // Different bytes per attempt, so the store keeps both rather than deduping them into
                // one and hiding the fact that there were two.
                Bytes = [.. Png, (byte)observation.Attempt]
            });

            return Task.CompletedTask;
        }

        public Task OnStepTimedOutAsync(StepObservation observation, RunContext run) => Task.CompletedTask;
    }

    /// <summary>Fails every time, so an observer has something to photograph twice.</summary>
    private sealed class FailingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Failing";
        public override string Description => "Throws every time.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("The warehouse said no.");

        public override Step<EmptyStepResultContext> Clone() => new FailingStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    /// <summary>Takes a picture on every attempt, failing the early ones so there is more than one.</summary>
    private sealed class CapturingStep(int failUntilAttempt = 0) : Step<EmptyStepResultContext>
    {
        private int attempts;

        public override string Name => "Capturing";

        public override string Description => "Takes a picture of what it saw.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new CapturingStep(failUntilAttempt).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Screenshot,
                Name = "page",
                Form = DebugPreviewForm.Image,
                Bytes = Png
            });

            if (++attempts < failUntilAttempt)
                throw new InvalidOperationException("Not yet.");

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class DocumentStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Document";

        public override string Description => "Keeps the markup it was looking at.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new DocumentStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Document,
                Name = "page-source",
                Form = DebugPreviewForm.Markup,
                Text = "<html><body>Checkout</body></html>"
            });

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class ComponentLogStep : Step<EmptyStepResultContext>
    {
        public override string Name => "ComponentLog";

        public override string Description => "Keeps what a component printed.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ComponentLogStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.LogStream,
                Name = "api-container-log",
                Form = DebugPreviewForm.Text,
                Text = "listening on :8080",
                Component = "api-container"
            });

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class WidgetRecordingDebugger : IRunDebugger, ISupportsWidgets
    {
        internal List<DebugWidgetEntry> Widgets { get; } = [];

        public bool IsCapturing => true;

        public Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
        {
            Widgets.Add(entry);
            return Task.CompletedTask;
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    /// <summary>A debugger from before widgets existed: it implements the interface and nothing else.</summary>
    private sealed class OldFashionedDebugger : IRunDebugger
    {
        internal bool SawSignals { get; private set; }

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        {
            SawSignals = true;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
