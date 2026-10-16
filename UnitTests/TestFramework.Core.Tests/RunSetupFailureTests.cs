using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// A run that fails while it is still being set up is over all the same: whatever watches it hears that it
/// ended, and whatever the run was handed to own is released.
/// </summary>
public class RunSetupFailureTests
{
    [Fact]
    public async Task ARunWhoseObserversCannotBeResolved_StillTellsItsDebuggerItFinished()
    {
        // The UI-visible symptom of a leak here is a run stuck in Running for the rest of the session.
        WatchedServices services = new WatchedServices();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SimpleTimeline().SetupRun(services).RunAsync());

        Assert.Equal("init", services.Debugger.Events[0]);
        Assert.Equal("finished", services.Debugger.Events[^1]);
        Assert.Contains("run:Error", services.Debugger.Events);
    }

    [Fact]
    public async Task ARunWhoseObserversCannotBeResolved_StillDisposesTheServicesItOwns()
    {
        WatchedServices services = new WatchedServices();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SimpleTimeline().SetupRun(RunServices.Owned(services)).RunAsync());

        Assert.True(services.Disposed);
    }

    private static Timeline SimpleTimeline() => Timeline.Create()
        .SetVariable("ready", true).Label("ready")
        .Build();

    /// <summary>Supplies a recording debugger, and an observer registration that fails the way a missing
    /// dependency of a real observer does.</summary>
    private sealed class WatchedServices : IServiceProvider, IDisposable
    {
        internal EventRecordingDebugger Debugger { get; } = new EventRecordingDebugger();

        internal bool Disposed { get; private set; }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRunDebugger))
                return Debugger;

            if (serviceType == typeof(IEnumerable<IStepObserver>))
                throw new InvalidOperationException("Unable to resolve a dependency of the registered step observer.");

            return null;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class EventRecordingDebugger : IRunDebugger
    {
        internal List<string> Events { get; } = [];

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start)
        {
            Events.Add("init");
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition)
        {
            if (transition.EntityKind == DebugEntityKind.Run)
                Events.Add($"run:{transition.State}");

            return Task.CompletedTask;
        }

        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId)
        {
            Events.Add("finished");
            return Task.CompletedTask;
        }

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
