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
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the failure payload attached to a step's outcome transition.
/// </summary>
/// <remarks>
/// Before this, a transition reported only that a step went red. The reason lived exclusively in
/// rendered log text, so a consumer had to scrape prose, and the framework's own recovery guidance
/// never reached a debugger at all.
/// </remarks>
public sealed class DebugFailureDetailTests
{
    [Fact]
    public async Task AFailingStepReportsItsExceptionOnTheTransition()
    {
        FailureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new ThrowingStep(new InvalidOperationException("boom")))
            .Name("explodes")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        DebugFailureDetail failure = Assert.Single(debugger.Failures);

        Assert.Equal(typeof(InvalidOperationException).FullName, failure.ExceptionType);
        Assert.Equal("boom", failure.Message);
        Assert.Equal(1, failure.Attempt);
        Assert.False(failure.WillRetry);
        Assert.False(failure.WasSuppressed);
        Assert.NotNull(failure.StackTrace);
    }

    [Fact]
    public async Task AFrameworkExceptionCarriesItsRecoveryGuidance()
    {
        // The point of the whole payload: the framework already knows what to suggest, and the UI
        // should be able to show it rather than the user re-reading a stack trace.
        FailureRecordingDebugger debugger = new();

        ArtifactNotFoundException expected = new("missing", ["found-one", "found-two"]);

        Timeline timeline = Timeline.Create()
            .Trigger(new ThrowingStep(expected))
            .Name("framework-failure")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        DebugFailureDetail failure = Assert.Single(debugger.Failures);

        Assert.Equal(expected.FriendlyMessage, failure.FriendlyMessage);
        Assert.NotEmpty(failure.RecoverySteps);
        Assert.Equal(expected.RecoverySteps, failure.RecoverySteps);
        Assert.Equal(expected.AvailableOptions, failure.AvailableOptions);
    }

    [Fact]
    public async Task ARetriedFailureMarksTheEarlierAttemptsAsRetrying()
    {
        FailureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new ThrowingStep(new InvalidOperationException("flaky")))
            .Name("retries")
            .WithRetry(2, CalcDelays.None)
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.Equal(3, debugger.Failures.Count);
        Assert.Equal([1, 2, 3], debugger.Failures.Select(f => f.Attempt));

        // Only the last attempt is final; the earlier ones must read as "a retry follows".
        Assert.True(debugger.Failures[0].WillRetry);
        Assert.True(debugger.Failures[1].WillRetry);
        Assert.False(debugger.Failures[2].WillRetry);
    }

    [Fact]
    public async Task ASuppressedFailureIsReportedAsSuppressedRatherThanHidden()
    {
        // Otherwise the UI shows a step that passed while an exception was thrown inside it, which
        // reads as a contradiction rather than as configured behaviour.
        FailureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new ThrowingStep(new InvalidOperationException("ignored")))
            .Name("suppressed")
            .ExpectExceptions(typeof(InvalidOperationException))
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        DebugFailureDetail failure = Assert.Single(debugger.Failures);

        Assert.True(failure.WasSuppressed);
        Assert.Equal("ignored", failure.Message);
    }

    [Fact]
    public async Task ASucceedingStepCarriesNoFailurePayload()
    {
        FailureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new PassingStep())
            .Name("fine")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.Empty(debugger.Failures);
    }

    [Fact]
    public void InnerExceptionsAreFlattenedOutermostFirst()
    {
        Exception exception = new InvalidOperationException(
            "outer",
            new ArgumentException("middle", new FormatException("inner")));

        DebugFailureDetail failure = DebugFailureDetail.Capture(exception, attempt: 1, willRetry: false, wasSuppressed: false)!;

        Assert.Collection(
            failure.InnerExceptions,
            first =>
            {
                Assert.Equal("System.ArgumentException", first.ExceptionType);
                Assert.Equal("middle", first.Message);
            },
            second =>
            {
                Assert.Equal("System.FormatException", second.ExceptionType);
                Assert.Equal("inner", second.Message);
            });
    }

    [Fact]
    public void CaptureOfNoExceptionIsNull()
        => Assert.Null(DebugFailureDetail.Capture(null, attempt: 1, willRetry: false, wasSuppressed: false));

    /// <summary>Hands the run a debugger the same way a consumer would: through DI.</summary>
    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class FailureRecordingDebugger : IRunDebugger
    {
        public List<DebugFailureDetail> Failures { get; } = [];

        public bool IsCapturing => true;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
        {
            if (failure is not null)
            {
                lock (Failures) Failures.Add(failure);
            }

            return Task.CompletedTask;
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    private sealed class ThrowingStep(Exception exception) : Step<EmptyStepResultContext>
    {
        public override string Name => "throwing";
        public override string Description => "Always throws.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => throw exception;

        public override Step<EmptyStepResultContext> Clone() => new ThrowingStep(exception).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class PassingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "passing";
        public override string Description => "Always succeeds.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new PassingStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
