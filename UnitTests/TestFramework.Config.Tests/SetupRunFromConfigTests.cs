using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;

namespace TestFramework.Config.Tests;

/// <summary>
/// A run set up from a configuration owns the services it was given, and is known by the test that started it.
/// </summary>
public class SetupRunFromConfigTests
{
    [Fact]
    public async Task WhatTheConfigurationRegistered_IsDisposedWhenTheRunEnds()
    {
        // The provider used to be built and handed over as a plain one, which a run never disposes - so every
        // singleton the configuration registered outlived the run.
        Holder holder = new();
        ConfigInstance config = ConfigInstance.Create()
            .AddService(services => services.AddSingleton(_ => holder.Created = new DisposableService()))
            .Build();

        TimelineRun run = await Timeline.Create()
            .Trigger(new ResolvesService())
            .Build()
            .SetupRun(config)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.NotNull(holder.Created);
        Assert.True(holder.Created!.Disposed, "A singleton the configuration registered outlived the run.");
    }

    [Fact]
    public async Task TheRunIsIdentifiedByTheTestThatStartedIt_NotByTheConfigurationPackage()
    {
        RecordingDebugger debugger = new();
        ConfigInstance config = ConfigInstance.Create()
            .AddService(services => services.AddSingleton<IRunDebugger>(debugger))
            .Build();

        await Timeline.Create().Build().SetupRun(config).RunAsync();

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);
        Assert.EndsWith(nameof(SetupRunFromConfigTests) + ".cs", identity.SourceFilePath!, StringComparison.Ordinal);
    }

    private sealed class Holder
    {
        public DisposableService? Created { get; set; }
    }

    private sealed class DisposableService : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>Resolves the service, so the singleton exists for the run to dispose.</summary>
    private sealed class ResolvesService : Step<EmptyStepResultContext>
    {
        public override string Name => "Resolves the service";
        public override string Description => "Resolves the service";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            _ = context.Services!.GetRequiredService<DisposableService>();
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new ResolvesService().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class RecordingDebugger : IRunDebugger
    {
        public TestIdentity? Identity { get; private set; }

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start)
        {
            Identity = start.Identity;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
