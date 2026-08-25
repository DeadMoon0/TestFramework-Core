using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a run tells its observers, and the guarantee that an observer cannot change what a step did.
/// </summary>
/// <remarks>
/// <para>
/// The hook exists so evidence gathering - a screenshot, the page markup, a held-open browser - is
/// registered once per package instead of living in a try/catch inside that package's own Execute, which
/// is how UI ended up with three of them and the next package would have copied all three.
/// </para>
/// <para>
/// That trade is only worth making if an observer can never decide an outcome, so most of what is asserted
/// here is what an observer is <em>not</em> able to do.
/// </para>
/// </remarks>
public class StepObserverTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AnObserverIsToldWhenAStepStartsAndWhenItFails()
    {
        RecordingObserver observer = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new FailingStep()).Name("checkout")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(observer), output).RunAsync();

        Assert.Equal(StepState.Error, run.Step("checkout").LastResult.State);
        Assert.Equal(["starting:checkout:1", "failed:checkout:1"], observer.CallsFor("checkout"));
        Assert.IsType<InvalidOperationException>(observer.LastFailure);
    }

    [Fact]
    public async Task AnObserverIsToldWhenAStepRunsOutOfTime()
    {
        RecordingObserver observer = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new SlowStep())
                .WithTimeOut(TimeSpan.FromMilliseconds(200)).Name("slow")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(observer), output).RunAsync();

        Assert.Equal(StepState.Timeout, run.Step("slow").LastResult.State);

        // Its own hook, not the failure one: a step that ran out of time and a step that threw need
        // different evidence, and telling them apart afterwards from an exception type is guesswork.
        Assert.Equal(["starting:slow:1", "timedout:slow:1"], observer.CallsFor("slow"));
    }

    [Fact]
    public async Task AnExceptionTheStepWasToldToIgnoreIsNotReportedAsAFailure()
    {
        // Otherwise an observer capturing evidence for every swallowed exception buries the failures
        // worth looking at under the ones the author already decided were fine.
        RecordingObserver observer = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new FailingStep())
                .ExpectExceptions(typeof(InvalidOperationException)).Name("tolerated")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(observer), output).RunAsync();

        Assert.Equal(StepState.Complete, run.Step("tolerated").LastResult.State);
        Assert.Equal(["starting:tolerated:1"], observer.CallsFor("tolerated"));
    }

    [Fact]
    public async Task AnObserverThatThrowsCannotTurnAGreenStepRed()
    {
        // A screenshot that failed to save is a worse thing to report than the run it was watching.
        Timeline timeline = Timeline.Create()
            .Trigger(new PassingStep()).Name("passes")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(new ThrowingObserver()), output).RunAsync();

        Assert.Equal(StepState.Complete, run.Step("passes").LastResult.State);
    }

    [Fact]
    public async Task AnObserverThatThrowsCannotTurnARedStepGreen()
    {
        // The direction that actually costs money: a failure that reports itself as a pass because the
        // thing watching it fell over.
        Timeline timeline = Timeline.Create()
            .Trigger(new FailingStep()).Name("fails")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(new ThrowingObserver()), output).RunAsync();

        StepResultGeneric result = run.Step("fails").LastResult;

        Assert.Equal(StepState.Error, result.State);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task AnObserverThatThrowsDoesNotStopTheNextOneFromWatching()
    {
        RecordingObserver second = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new PassingStep()).Name("passes")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(new CollectionProvider([new ThrowingObserver(), second]), output)
            .RunAsync();

        Assert.Equal(StepState.Complete, run.Step("passes").LastResult.State);
        Assert.Equal(["starting:passes:1"], second.CallsFor("passes"));
    }

    [Fact]
    public async Task EachRetryIsItsOwnAttemptToWhoeverIsWatching()
    {
        // An observer that captures evidence needs to know which attempt it is looking at, or the second
        // screenshot overwrites the first and the interesting one is gone.
        RecordingObserver observer = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new FailingStep())
                .WithRetry(1, CalcDelays.None).Name("retried")
            .Build();

        TimelineRun run = await timeline.SetupRun(Registered(observer), output).RunAsync();

        Assert.Equal(StepState.Error, run.Step("retried").LastResult.State);
        Assert.Equal(
            ["starting:retried:1", "failed:retried:1", "starting:retried:2", "failed:retried:2"],
            observer.CallsFor("retried"));
    }

    [Fact]
    public async Task TheStepsTheFrameworkInsertsAreObservedToo()
    {
        // Every step, including the ones nobody wrote: the artifact teardown a run appends is exactly the
        // kind of step you want evidence from when it fails, and filtering is the observer's business.
        // There is deliberately no "this one is ours" flag, because the obvious proxy for it is wrong -
        // half of Core's system steps are steps the author asked for through the fluent API.
        RecordingObserver observer = new RecordingObserver();

        Timeline timeline = Timeline.Create()
            .Trigger(new PassingStep()).Name("passes")
            .Build();

        await timeline.SetupRun(Registered(observer), output).RunAsync();

        Assert.Contains("starting:Deconstruct All Artifacts:1", observer.Calls);
    }

    [Fact]
    public void AnObserverRegisteredBothWaysIsStillOnlyToldOnce()
    {
        // A container that answers the single question and the collection question with the same instance
        // is normal; watching a step twice would double every screenshot it takes.
        RecordingObserver observer = new RecordingObserver();

        StepObservers observers = StepObservers.For(new BothWaysProvider(observer));
        observers.Starting(Observation(), new ScopedLogger(null));

        Assert.Equal(["starting:checkout:1"], observer.Calls);
    }

    [Fact]
    public void NoObserversIsTheNormalCase()
    {
        // Nothing registered, and nothing to resolve: the runner still has one thing to call.
        Assert.Same(StepObservers.None, StepObservers.For(null));
        Assert.Same(StepObservers.None, StepObservers.For(new CollectionProvider([])));

        StepObservers.None.Starting(Observation(), new ScopedLogger(null));
    }

    private static StepObservation Observation() => new StepObservation("checkout", "Checkout", "Main Stage", 1);

    private static IServiceProvider Registered(IStepObserver observer) => new CollectionProvider([observer]);

    /// <summary>Answers the collection question only, the way a container registering many does.</summary>
    private sealed class CollectionProvider(IReadOnlyList<IStepObserver> observers) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IStepObserver>) ? observers : null;
    }

    /// <summary>Answers both questions with the same instance.</summary>
    private sealed class BothWaysProvider(IStepObserver observer) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<IStepObserver>))
                return new[] { observer };

            return serviceType == typeof(IStepObserver) ? observer : null;
        }
    }

    private sealed class RecordingObserver : IStepObserver
    {
        private readonly object syncRoot = new object();
        private readonly List<string> calls = [];

        public IReadOnlyList<string> Calls
        {
            get { lock (syncRoot) { return [.. calls]; } }
        }

        public Exception? LastFailure { get; private set; }

        /// <summary>
        /// The calls about one step. A run observes every step it executes, its own included, so a test
        /// about one step says which one rather than pinning the whole run's traffic.
        /// </summary>
        /// <param name="label">The step's label.</param>
        /// <returns>The calls.</returns>
        public IReadOnlyList<string> CallsFor(string label)
        {
            lock (syncRoot)
            {
                return [.. calls.Where(call => call.Contains($":{label}:", StringComparison.Ordinal))];
            }
        }

        public void OnStepStarting(StepObservation observation) => Record("starting", observation);

        public void OnStepFailed(StepObservation observation, Exception exception)
        {
            LastFailure = exception;
            Record("failed", observation);
        }

        public void OnStepTimedOut(StepObservation observation) => Record("timedout", observation);

        private void Record(string hook, StepObservation observation)
        {
            lock (syncRoot)
            {
                calls.Add($"{hook}:{observation.Label}:{observation.Attempt}");
            }
        }
    }

    private sealed class ThrowingObserver : IStepObserver
    {
        public void OnStepStarting(StepObservation observation) => throw new InvalidOperationException("The screenshot directory is gone.");

        public void OnStepFailed(StepObservation observation, Exception exception) => throw new InvalidOperationException("The browser is already closed.");

        public void OnStepTimedOut(StepObservation observation) => throw new InvalidOperationException("Nothing left to photograph.");
    }

    private sealed class PassingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Passing";

        public override string Description => "Returns immediately.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new PassingStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    private sealed class FailingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Failing";

        public override string Description => "Throws every time.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new FailingStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("The warehouse said no.");
    }

    private sealed class SlowStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Slow";

        public override string Description => "Outlives any deadline worth setting in a test.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new SlowStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override async Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.Deadline.Token);

            return EmptyStepResultContext.Instance;
        }
    }
}
