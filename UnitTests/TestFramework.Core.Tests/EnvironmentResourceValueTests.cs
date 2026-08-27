using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// A component publishing what it started, and a step reading it.
/// </summary>
/// <remarks>
/// <para>
/// This chain had never run. The graph's producer half - <c>NodeContext.Produce</c> - was only reachable from
/// a <c>ProvisionedResourceNode</c>, and nothing constructed a <c>NodeContext</c> or called one, so the half
/// that could publish had no driver. What actually starts containers is an <see cref="EnvComponent"/>, and a
/// component was handed a context whose values are read-only. Two lifecycles, one able to publish and never
/// driven, one driven and unable to publish.
/// </para>
/// <para>
/// The consequence was not theoretical: three packages published a started resource's address by writing it
/// back into another package's configuration store, because from a component that was the only channel that
/// existed. These cases pin the channel that replaces it, including who is not allowed to use it.
/// </para>
/// </remarks>
public class EnvironmentResourceValueTests(ITestOutputHelper output)
{
    private static readonly ResourceKind Queue = ResourceKind
        .Named("test.queue")
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers("QueueName")
        .Build();

    [Fact]
    public async Task AComponentPublishesWhatItStartedAndAStepReadsIt()
    {
        // The whole point, end to end: nobody wrote this address down, a component learned it by starting
        // something, and a later step asks the run for it rather than for somebody's configuration record.
        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(null, output)
            .SetEnv(new PublishingEnvironment())
            .RunAsync();

        Assert.Equal(StepState.Complete, run.Step("reads").LastResult.State);
        Assert.Equal("host-connection", run.VariableStore.GetVariable<string>("seen"));
    }

    [Fact]
    public async Task AnOrdinaryStepIsGivenNoChannelAtAll()
    {
        // Null rather than empty, and that is the guarantee: a step that could publish a resource value could
        // point a passing test at a different system than the one it was written to prove.
        Timeline timeline = Timeline.Create()
            .Trigger(new ChannelInspectingStep()).Name("inspects")
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).RunAsync();

        Assert.Equal(StepState.Complete, run.Step("inspects").LastResult.State);
        Assert.False(run.VariableStore.GetVariable<bool>("could-publish"));
    }

    [Fact]
    public async Task PublishingSomethingTheKindDoesNotOfferIsRefused()
    {
        // The kind is what plan-time validation checked routes against, so producing outside it would make
        // that validation a promise the run does not keep.
        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(null, output)
            .SetEnv(new PublishingEnvironment(valueName: "NotOffered"))
            .RunAsync();

        // Asserted on the run rather than on the step: the component step is Core's own and carries no label
        // a test can address, which is right - it is not part of anybody's plan to name.
        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("does not offer", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValueOfferedPerViewpointCannotBePublishedWithoutOne()
    {
        // What the test process reaches is not what a peer container reaches, so a kind that says so has to
        // be published that way - otherwise one of the two readers silently gets the other's coordinate.
        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(null, output)
            .SetEnv(new PublishingEnvironment(withoutVantage: true))
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("without a viewpoint", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AComponentAskedForItsChannelOutsideAnyRunSaysSoRatherThanDoingNothing()
    {
        // The whole reason the non-nullable accessor exists. A publish into a null channel reads harmlessly
        // and is not: the reader then falls back to a declared default, and a container's default port is
        // not where the container is - so the run dials nothing and looks like a hang.
        FrameworkStateException failure = Assert.Throws<FrameworkStateException>(
            static () => new PublishingComponent(ValueNames.ConnectionString, withoutVantage: false).ChannelOutsideARun());

        Assert.Contains("framework fault", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An environment whose one component publishes a coordinate it "started".</summary>
    private sealed class PublishingEnvironment : EnvironmentProviderBase
    {
        private readonly EnvComponentIdentifier component = "publishing-queue";

        public PublishingEnvironment(string valueName = ValueNames.ConnectionString, bool withoutVantage = false)
        {
            this.AddComponent(new PublishingComponent(valueName, withoutVantage));
        }

        /// <summary>
        /// Always creates the component. A real environment resolves what the steps require; this one has a
        /// single component and the point of the test is what happens once it runs.
        /// </summary>
        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
            => [this.component];
    }

    private sealed class PublishingComponent(string valueName, bool withoutVantage) : EnvComponent
    {
        public override EnvComponentIdentifier Id => "publishing-queue";

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            EnvironmentResources resources = PublishOn(context);

            if (withoutVantage)
            {
                resources.Produce(Queue, "orders", valueName, "host-connection");
            }
            else
            {
                resources.Produce(Queue, "orders", valueName, ResourceVantage.Host, "host-connection");
                resources.Produce(Queue, "orders", valueName, ResourceVantage.Network, "network-connection");
            }

            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;

        /// <summary>Asks for the channel from a context that is nobody's component creation.</summary>
        internal EnvironmentResources ChannelOutsideARun() => PublishOn(RunContext.Detached());
    }

    private sealed class ReadingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Reading";

        public override string Description => "Asks the run where a resource ended up.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ReadingStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable(
                "seen",
                context.Values.Require(ValueRef.For(Queue.Name, "orders", ValueNames.ConnectionString), ResourceVantage.Host));

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class ChannelInspectingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "Inspecting";

        public override string Description => "Records whether it was handed a way to publish.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ChannelInspectingStep().WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable("could-publish", context.Resources is not null);

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
