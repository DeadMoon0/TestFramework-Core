using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a run lets a package keep for the length of it, and why the run has to be the one saying which
/// run that is.
/// </summary>
/// <remarks>
/// <para>
/// The channels a run offers - variables and artifacts - carry data. A browser session is not data: it
/// cannot be serialised, it has to be closed, and the whole point of it is that the page one step leaves
/// behind is the page the next step finds. Before this existed, UI kept its sessions in a table keyed on
/// the run's variable store, reasoning that the store is the one object every step of a run shares.
/// </para>
/// <para>
/// That reasoning was correct and then quietly stopped being: a step is handed a per-attempt view of the
/// store, not the store. These tests pin the three places that inference broke, because each of them
/// fails silently - a second browser, an uncleaned first one, and a cleanup step that closes nothing.
/// </para>
/// </remarks>
public class RunStateTests(ITestOutputHelper output)
{
    [Fact]
    public void OneSlotPerTypePerRun()
    {
        RunState state = new RunState();

        Sessions first = state.GetOrAdd(() => new Sessions());
        Sessions second = state.GetOrAdd(() => new Sessions());

        Assert.Same(first, second);
    }

    [Fact]
    public void AskingWhetherARunHasStateDoesNotGiveItSome()
    {
        // A cleanup step must not open a browser in order to find out that no browser was opened.
        RunState state = new RunState();

        Assert.False(state.TryGet(out Sessions? absent));
        Assert.Null(absent);

        state.GetOrAdd(() => new Sessions());

        Assert.True(state.TryGet(out Sessions? present));
        Assert.NotNull(present);
    }

    [Fact]
    public void TwoStepsReachingItAtOnceGetTheSameInstance()
    {
        // The guarantee, not an optimisation: a run that opened two browsers because two steps asked at the
        // same moment would be worse than one that opened none.
        RunState state = new RunState();
        int built = 0;

        Sessions[] found = [.. Enumerable
            .Range(0, 32)
            .AsParallel()
            .Select(_ => state.GetOrAdd(() =>
            {
                System.Threading.Interlocked.Increment(ref built);

                return new Sessions();
            }))];

        Assert.Equal(1, built);
        Assert.All(found, session => Assert.Same(found[0], session));
    }

    [Fact]
    public async Task ARetryFindsWhatTheAttemptBeforeItLeftBehind()
    {
        // The trap this type exists for. A step is handed a per-attempt view of the variable store, so a
        // package keying its own table on what it was handed gets a fresh table on every retry - and opens
        // a second browser while the first is still running.
        Timeline timeline = Timeline.Create()
            .Trigger(new RecordingStep(failFirst: true))
                .WithRetry(1, CalcDelays.None).Name("twice")
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).RunAsync();

        Sessions sessions = run.VariableStore.RunState.GetOrAdd(() => new Sessions());

        Assert.Equal(["twice attempt 1", "twice attempt 2"], sessions.Opened);
    }

    [Fact]
    public async Task TheCleanupStepFindsWhatTheStepBeforeItOpened()
    {
        // The other half, and the one that leaks: a cleanup step reading a state of its own would report
        // that it closed everything, having closed nothing.
        Timeline timeline = Timeline.Create()
            .Trigger(new RecordingStep(failFirst: false)).Name("opens")
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).RunAsync();

        Sessions sessions = run.VariableStore.RunState.GetOrAdd(() => new Sessions());

        Assert.Equal(["opens attempt 1"], sessions.Opened);
        Assert.Equal(["opens attempt 1"], sessions.Closed);
    }

    /// <summary>Stands in for a package's live per-run things.</summary>
    private sealed class Sessions
    {
        private readonly List<string> opened = [];
        private readonly List<string> closed = [];

        public IReadOnlyList<string> Opened => [.. this.opened];

        public IReadOnlyList<string> Closed => [.. this.closed];

        public void Open(string what) => this.opened.Add(what);

        public void CloseAll()
        {
            this.closed.AddRange(this.opened);
        }
    }

    private sealed class RecordingStep(bool failFirst) : Step<EmptyStepResultContext>, IHasCleanupStep
    {
        public override string Name => "Recording";

        public override string Description => "Records itself in the run's state.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new RecordingStep(failFirst).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        /// <summary>
        /// Offered at plan time, where the run's own store is what is on hand rather than a step's view of
        /// it - which is the second reason the state cannot hang off whatever a caller happens to hold.
        /// </summary>
        /// <param name="variableStore">The run's variables.</param>
        /// <returns>The cleanup step.</returns>
        public StepGeneric? CreateCleanupStep(VariableStore variableStore) => new ClosingStep();

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.State
                .GetOrAdd(() => new Sessions())
                .Open($"{context.Attempt?.Label} attempt {context.Attempt?.Number}");

            // Fails once, so the retry has something to find.
            if (failFirst && context.Attempt?.Number == 1)
                throw new InvalidOperationException("Not yet.");

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class ClosingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Closing";

        public override string Description => "Closes what the run opened.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ClosingStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            if (context.State.TryGet(out Sessions? sessions) && sessions is not null)
                sessions.CloseAll();

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
