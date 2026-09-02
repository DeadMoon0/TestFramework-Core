using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers a watching consumer asking a run for a fresh look at itself.
/// </summary>
/// <remarks>
/// Everything else a run reports is something it decided to say. This is the one message that arrives
/// from outside and asks the run to do something, and the moment it matters is the moment a step is
/// held at a breakpoint: the browser is sitting on a page whose last photograph was taken before the
/// step that got there had finished, so what a reader is looking at is always one step stale.
/// </remarks>
public sealed class WidgetCaptureTests : IDisposable
{
    /// <summary>A one-pixel PNG, so the evidence is a real picture rather than bytes pretending.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>
    /// How long a wait for something that should already be happening lasts.
    /// </summary>
    /// <remarks>
    /// Generous, because what is asserted is order rather than latency: on a runner executing
    /// collections in parallel a tight budget only turns CPU contention into a false failure.
    /// </remarks>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly string root = Path.Combine(Path.GetTempPath(), "tf-capture-" + Guid.NewGuid().ToString("N"));
    private readonly string? previousOutput = System.Environment.GetEnvironmentVariable(RunOutput.DirectoryVariable);

    public WidgetCaptureTests()
        => System.Environment.SetEnvironmentVariable(RunOutput.DirectoryVariable, root);

    [Fact]
    public async Task EvidenceCapturedWhileAStepIsHeldIsFiledAgainstThatStep()
    {
        // The whole point of the feature. A capture asked for over the wire is served on the
        // transport's own thread, which is inside no step and can read no execution context - so
        // without the run remembering where it stopped, the fresh picture would arrive attributed to
        // nothing and appear on no step in any consumer showing it.
        DebuggerHoldingOneStep debugger = new();
        ScreenshotSource source = new();

        using JournalScope journal = JournalScope.Disarmed();

        Task run = Timeline.Create()
            .Trigger(new QuietStep())
            .Name("checkout")
            .Build()
            .SetupRun(new RunServices(debugger, source))
            .RunAsync();

        await debugger.Held.WaitAsync(Patience);

        WidgetCaptureOutcome outcome = await debugger.Ask().WaitAsync(Patience);

        debugger.Release();
        await run.WaitAsync(Patience);

        Assert.Equal(1, outcome.Captured);
        Assert.Null(outcome.Detail);

        DebugWidgetEntry entry = Assert.Single(debugger.Widgets);

        Assert.Equal(debugger.HeldStage, entry.Stage);
        Assert.Equal(debugger.HeldStep, entry.StepId);

        // No attempt, and that is the truth rather than an omission: a breakpoint holds a step
        // before it begins, so there is no attempt for this picture to belong to.
        Assert.Null(entry.Attempt);

        // Ordinary in every other respect. It went out as a widget signal like any other and was
        // written beside the run's own evidence, which is why a journal replays it with no special
        // case and a bundle collects it without knowing it was asked for.
        Assert.Equal(WidgetKinds.Screenshot, entry.Kind);
        Assert.Equal("widgets/live.png", entry.Description.Body!.RelativePath);
        Assert.Equal(Png, File.ReadAllBytes(entry.Description.Body.Path));
    }

    [Fact]
    public async Task TheAnswerWaitsForTheEvidenceItCounts()
    {
        // The answer and the evidence leave by different roads: a widget is queued and delivered by
        // the one reader that drains the run's signals, while the ack goes straight out on the
        // transport. So an asker could be told "one captured" and find nothing where it was told to
        // look, having been answered before the picture it was about had been sent at all.
        DebuggerHoldingOneStep debugger = new();
        ScreenshotSource source = new();

        using JournalScope journal = JournalScope.Disarmed();

        debugger.HoldDelivery();

        Task run = Timeline.Create()
            .Trigger(new QuietStep())
            .Name("checkout")
            .Build()
            .SetupRun(new RunServices(debugger, source))
            .RunAsync();

        await debugger.Held.WaitAsync(Patience);

        Task<WidgetCaptureOutcome> answer = debugger.Ask();

        await debugger.Delivering.WaitAsync(Patience);
        Assert.False(answer.IsCompleted, "The run answered before the picture it was answering about had been delivered.");

        debugger.FinishDelivery();

        WidgetCaptureOutcome outcome = await answer.WaitAsync(Patience);

        Assert.Equal(1, outcome.Captured);
        Assert.Single(debugger.Widgets);

        debugger.Release();
        await run.WaitAsync(Patience);
    }

    [Fact]
    public async Task AFinishedRunRefusesToBePhotographedRatherThanWritingIntoItself()
    {
        // A finished run is a snapshot that can be handed around and trusted. Capturing into one
        // would write a file into its output while the signal describing it was dropped on a closed
        // queue - evidence the run itself says nothing about, which makes the snapshot quietly
        // incomplete rather than obviously wrong.
        DebuggerHoldingOneStep debugger = new();
        ScreenshotSource source = new();

        using JournalScope journal = JournalScope.Disarmed();

        // Released before it is asked, so the run is over by the time the request arrives.
        debugger.Release();

        await Timeline.Create()
            .Trigger(new QuietStep())
            .Name("checkout")
            .Build()
            .SetupRun(new RunServices(debugger, source))
            .RunAsync()
            .WaitAsync(Patience);

        WidgetCaptureOutcome outcome = await debugger.Ask().WaitAsync(Patience);

        Assert.Equal(0, outcome.Captured);
        Assert.Contains("finished", outcome.Detail!, StringComparison.OrdinalIgnoreCase);

        // And nothing was written either: the refusal comes before the capture rather than after it.
        // A run that never wrote anything has no output directory at all, which is the strongest form
        // of this the test can see.
        Assert.Empty(Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "live.png", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task ACaptureIsFiledAgainstTheRunWhenTwoStepsAreWaitingAtOnce()
    {
        // Every step passes through the waiting list on its way in, held or not, so two entries mean
        // either two steps genuinely paused or one paused and one merely asking. Neither has a single
        // right answer, and filing the picture on a step that has nothing to do with it is worse than
        // filing it against the run - a reader can tell an unattributed picture from a lie.
        WidgetRecordingDebugger debugger = new();
        DebuggingRunSession session = new(debugger);

        await session.InitSessionAsync(EmptyStructure);

        Task first = session.WaitWhenBreakpointHit("Main", 0);
        Task second = session.WaitWhenBreakpointHit("Main", 1);

        await debugger.TwoWaiting.WaitAsync(Patience);

        session.PublishWidget(WidgetKinds.Screenshot, "live", component: null, PictureDescription);

        debugger.ReleaseAll();
        await Task.WhenAll(first, second).WaitAsync(Patience);
        await session.FinishSessionAsync();

        DebugWidgetEntry entry = Assert.Single(debugger.Widgets);

        Assert.Null(entry.Stage);
        Assert.Null(entry.StepId);
    }

    [Fact]
    public async Task ARunWithNothingToCaptureWithSaysSoRatherThanLeavingTheAskerWaiting()
    {
        // A consumer that pressed a button is owed a reply. Silence would cost it the full wait to
        // learn what it could have been told at once: this run has nothing that takes pictures.
        string pipeName = "tf-capture-" + Guid.NewGuid().ToString("N");

        using PipeScope scope = PipeScope.PointedAt(pipeName);
        using CancellationTokenSource life = new(TimeSpan.FromMinutes(2));

        await using FakeConsumer consumer = FakeConsumer.Listening(pipeName, life.Token);
        using PipeRunDebugger debugger = new();

        // Something has to go out first, because the transport connects lazily.
        await debugger.SignalLogEntryAsync("session-1", NoteEntry);

        await consumer.SendAsync(new PipeCaptureWidgetRequestSignal { SessionId = "session-1" });

        PipeCaptureWidgetAckSignal ack = await consumer.NextAckAsync().WaitAsync(Patience);

        Assert.Equal(0, ack.Captured);
        Assert.False(string.IsNullOrWhiteSpace(ack.Detail));
    }

    [Fact]
    public async Task TheRunKeepsHearingItsConsumerWhileACaptureIsInFlight()
    {
        // Served off the receive loop, and this is why. A capture photographs a live page, and the
        // loop it would otherwise be blocking is the only thing that can hear the release for the very
        // breakpoint the request came from - so serving the picture on it would hold the run in order
        // to get a look at the run.
        string pipeName = "tf-capture-" + Guid.NewGuid().ToString("N");

        using PipeScope scope = PipeScope.PointedAt(pipeName);
        using CancellationTokenSource life = new(TimeSpan.FromMinutes(2));

        await using FakeConsumer consumer = FakeConsumer.Listening(pipeName, life.Token);
        using PipeRunDebugger debugger = new();

        TaskCompletionSource capturing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finishCapture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        ((ISupportsWidgetCaptureRequests)debugger).OnCaptureRequested = async () =>
        {
            capturing.TrySetResult();
            await finishCapture.Task;
            return new WidgetCaptureOutcome(1, null);
        };

        ((ISupportsRunCancellation)debugger).CancellationRequested += _ => stopped.TrySetResult();

        await debugger.SignalLogEntryAsync("session-1", NoteEntry);

        await consumer.SendAsync(new PipeCaptureWidgetRequestSignal { SessionId = "session-1" });
        await capturing.Task.WaitAsync(Patience);

        // Sent while the capture is still going. On the old shape - answering on the loop - this
        // would not be read until the picture was finished, which for a held browser could be never.
        await consumer.SendAsync(new PipeCancelRunSignal { SessionId = "session-1", Reason = "Stopped." });

        await stopped.Task.WaitAsync(Patience);

        finishCapture.TrySetResult();

        PipeCaptureWidgetAckSignal ack = await consumer.NextAckAsync().WaitAsync(Patience);
        Assert.Equal(1, ack.Captured);
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

    private static TimelineRunStructure EmptyStructure => new()
    {
        Stages = [],
        Variables = new Dictionary<VariableIdentifier, DebugValue>(),
        Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
    };

    private static DebugValueDescription PictureDescription => new()
    {
        Summary = "live",
        Shape = DebugValueShape.Binary
    };

    private static DebugLogEntry NoteEntry => new()
    {
        Level = DebugLogLevel.Information,
        EventName = "connected",
        Template = "connected",
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    /// <summary>What the browser pack registers, reduced to the part that matters here.</summary>
    private sealed class ScreenshotSource : IWidgetCaptureSource
    {
        public Task CaptureAsync(RunContext run)
        {
            run.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Screenshot,
                Name = "live",
                Form = DebugPreviewForm.Image,
                Bytes = Png
            });

            return Task.CompletedTask;
        }
    }

    private sealed class RunServices(IRunDebugger debugger, IWidgetCaptureSource source) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRunDebugger))
                return debugger;

            return serviceType == typeof(IWidgetCaptureSource) ? source : null;
        }
    }

    private sealed class QuietStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Quiet";

        public override string Description => "Does nothing, so the breakpoint is the only event.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new QuietStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    /// <summary>Holds the first step that asks, and records the evidence the run reports.</summary>
    private sealed class DebuggerHoldingOneStep : IRunDebugger, ISupportsWidgets, ISupportsWidgetCaptureRequests
    {
        private readonly TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource delivering = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DebugWidgetEntry> widgets = [];
        private int holding;

        /// <summary>Set while a test wants to stand in the middle of a widget being delivered.</summary>
        private TaskCompletionSource? deliveryGate;

        public bool IsCapturing => true;

        public Func<Task<WidgetCaptureOutcome>>? OnCaptureRequested { get; set; }

        /// <summary>Completes once a step is being held.</summary>
        internal Task Held => held.Task;

        internal string? HeldStage { get; private set; }

        internal int? HeldStep { get; private set; }

        internal IReadOnlyList<DebugWidgetEntry> Widgets
        {
            get
            {
                lock (widgets)
                    return [.. widgets];
            }
        }

        /// <summary>Asks for a capture the way the transport does when a consumer requests one.</summary>
        internal Task<WidgetCaptureOutcome> Ask()
        {
            Func<Task<WidgetCaptureOutcome>> capture = Assert.IsAssignableFrom<Func<Task<WidgetCaptureOutcome>>>(OnCaptureRequested);
            return capture();
        }

        internal void Release() => release.TrySetResult();

        /// <summary>Completes when a widget has arrived but has not been recorded yet.</summary>
        internal Task Delivering => delivering.Task;

        /// <summary>Stops the next widget half way, so the moment before it lands can be looked at.</summary>
        internal void HoldDelivery() => deliveryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void FinishDelivery() => deliveryGate?.TrySetResult();

        public async Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
        {
            if (Interlocked.Exchange(ref holding, 1) == 1)
                return;

            HeldStage = stage;
            HeldStep = stepId;
            held.TrySetResult();

            await release.Task;
        }

        public async Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
        {
            delivering.TrySetResult();

            if (deliveryGate is { } gate)
                await gate.Task;

            lock (widgets)
                widgets.Add(entry);
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
    }

    /// <summary>Holds every step that asks, so more than one can be waiting at a time.</summary>
    private sealed class WidgetRecordingDebugger : IRunDebugger, ISupportsWidgets
    {
        private readonly TaskCompletionSource twoWaiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DebugWidgetEntry> widgets = [];
        private int waiting;

        public bool IsCapturing => true;

        /// <summary>Completes once two steps are waiting together.</summary>
        internal Task TwoWaiting => twoWaiting.Task;

        internal IReadOnlyList<DebugWidgetEntry> Widgets
        {
            get
            {
                lock (widgets)
                    return [.. widgets];
            }
        }

        internal void ReleaseAll() => release.TrySetResult();

        public async Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
        {
            if (Interlocked.Increment(ref waiting) == 2)
                twoWaiting.TrySetResult();

            await release.Task;
        }

        public Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
        {
            lock (widgets)
                widgets.Add(entry);

            return Task.CompletedTask;
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
    }

    /// <summary>
    /// Points the transport at a pipe of this test's own.
    /// </summary>
    /// <remarks>
    /// A name per test rather than the well-known one, so a developer with the real UI open does not
    /// have their debugger answer these requests - and so this test cannot answer their run's.
    /// </remarks>
    private sealed class PipeScope : IDisposable
    {
        private const string NameVariable = "TESTFRAMEWORK_DEBUG_PIPE_NAME";
        private const string ModeVariable = "TESTFRAMEWORK_DEBUG_PIPE";

        private readonly string? previousName;
        private readonly string? previousMode;

        private PipeScope(string? previousName, string? previousMode)
        {
            this.previousName = previousName;
            this.previousMode = previousMode;
        }

        internal static PipeScope PointedAt(string pipeName)
        {
            PipeScope scope = new(
                System.Environment.GetEnvironmentVariable(NameVariable),
                System.Environment.GetEnvironmentVariable(ModeVariable));

            System.Environment.SetEnvironmentVariable(NameVariable, pipeName);
            System.Environment.SetEnvironmentVariable(ModeVariable, "on");

            return scope;
        }

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(NameVariable, previousName);
            System.Environment.SetEnvironmentVariable(ModeVariable, previousMode);
        }
    }

    /// <summary>A consumer that can ask a run for evidence and read what comes back.</summary>
    private sealed class FakeConsumer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream server;
        private readonly CancellationTokenSource life;
        private readonly TaskCompletionSource<PipeProtocolStream> connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly System.Collections.Concurrent.ConcurrentQueue<PipeCaptureWidgetAckSignal> acks = new();
        private readonly SemaphoreSlim arrived = new(0);
        private readonly Task pump;

        private FakeConsumer(NamedPipeServerStream server, CancellationTokenSource life)
        {
            this.server = server;
            this.life = life;

            pump = PumpAsync(life.Token);
        }

        internal static FakeConsumer Listening(string pipeName, CancellationToken cancellationToken)
        {
            NamedPipeServerStream server = new(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            return new FakeConsumer(server, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        }

        internal async Task SendAsync(IPipeSignal signal)
        {
            PipeProtocolStream framing = await connected.Task;
            await framing.SendSignalAsync(signal);
        }

        /// <summary>The next answer to a capture request, waiting for it if it has not arrived.</summary>
        internal async Task<PipeCaptureWidgetAckSignal> NextAckAsync()
        {
            await arrived.WaitAsync(life.Token);

            Assert.True(acks.TryDequeue(out PipeCaptureWidgetAckSignal? ack));
            return ack!;
        }

        public async ValueTask DisposeAsync()
        {
            await life.CancelAsync();

            try
            {
                await pump;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            life.Dispose();
            arrived.Dispose();
            await server.DisposeAsync();
        }

        private async Task PumpAsync(CancellationToken cancellationToken)
        {
            try
            {
                await server.WaitForConnectionAsync(cancellationToken);

                PipeProtocolStream framing = new(server);
                connected.TrySetResult(framing);

                while (!cancellationToken.IsCancellationRequested)
                {
                    IPipeSignal? signal = await framing.WaitSignalAsync(cancellationToken);

                    if (signal is null)
                        break;

                    if (signal is not PipeCaptureWidgetAckSignal ack)
                        continue;

                    acks.Enqueue(ack);
                    arrived.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
