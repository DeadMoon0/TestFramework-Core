using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Tests;

/// <summary>
/// A run builder owns one run's stores. Reusing it used to fail deep inside the run with a message
/// about frozen state; it should say plainly that the builder is spent.
/// </summary>
public class TimelineRunBuilderReuseTests
{
    [Fact]
    public async Task RunAsync_Twice_ReportsThatTheBuilderIsSpent()
    {
        ITimelineRunBuilder builder = Timeline.Create().Build().SetupRun();
        await builder.RunAsync();

        TimelineRunBuilderAlreadyUsedException exception =
            await Assert.ThrowsAsync<TimelineRunBuilderAlreadyUsedException>(() => builder.RunAsync());

        Assert.Contains("SetupRun", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddVariable_AfterRun_ReportsThatTheBuilderIsSpent()
    {
        ITimelineRunBuilder builder = Timeline.Create().Build().SetupRun();
        await builder.RunAsync();

        Assert.Throws<TimelineRunBuilderAlreadyUsedException>(() => builder.AddVariable("late", "value"));
    }

    [Fact]
    public async Task AFailedRun_AlsoInvalidatesTheBuilder()
    {
        ITimelineRunBuilder builder = Timeline.Create()
            .Trigger(new ThrowingStep())
            .Build()
            .SetupRun();

        await builder.RunAsync();

        Assert.Throws<TimelineRunBuilderAlreadyUsedException>(() => builder.AddVariable("late", "value"));
        await Assert.ThrowsAsync<TimelineRunBuilderAlreadyUsedException>(() => builder.RunAsync());
    }

    private sealed class ThrowingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "throwing";
        public override string Description => "always fails";
        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ThrowingStep().WithClonedOptions(this);

        public override Task<EmptyStepResultContext?> Execute(
            IServiceProvider serviceProvider,
            VariableStore variableStore,
            ArtifactStore artifactStore,
            ScopedLogger logger,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }
    }
}
