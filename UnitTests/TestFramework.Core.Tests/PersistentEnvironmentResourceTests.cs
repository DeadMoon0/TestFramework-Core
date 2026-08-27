using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// A component that runs once, and the runs that reuse it.
/// </summary>
/// <remarks>
/// <para>
/// A persistent component's body runs in a bootstrap and never again: every later run is handed a stand-in
/// that returns the same state without running anything. State travelled that way from the start. The
/// addresses the body worked out did not, and they cannot be worked out again - a container's port is chosen
/// by the operating system when it starts, and is knowable nowhere else.
/// </para>
/// <para>
/// That gap is why the first attempt at moving components off writing into configuration stores broke the
/// persistent path rather than the per-run one, and did it quietly: the publish went nowhere, a reader fell
/// back to the placeholder in the configuration file, and a step spent its whole timeout dialling a default
/// port nothing answered on. These cases pin the carry-over, including that it beats the placeholder.
/// </para>
/// </remarks>
public class PersistentEnvironmentResourceTests(ITestOutputHelper output)
{
    private static readonly ResourceKind Emulator = ResourceKind
        .Named("test.emulator")
        .OffersPerVantage(ValueNames.ConnectionString)
        .Build();

    /// <summary>A resource an environment can say something about without starting anything.</summary>
    private static readonly ResourceKind Declared = ResourceKind
        .Named("test.declared")
        .OffersPerVantage(ValueNames.ConnectionString)
        .Build();

    /// <summary>
    /// How many times a component body has actually run. Static because the bootstrap and each run are
    /// handed their own environment instance, which is the very thing being measured.
    /// </summary>
    private static int starts;

    /// <summary>How many times a component body has been asked to take itself down.</summary>
    private static int teardowns;

    [Fact]
    public async Task AReusedComponentGivesEveryRunTheAddressItWorkedOutOnce()
    {
        // The address is deliberately unguessable: it counts starts, so a run that saw the body run again
        // reads a different number, and a run that saw nothing at all cannot read anything.
        Interlocked.Exchange(ref starts, 0);

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        await using PersistentEnvironmentContext<EmulatorSetup> persistent =
            await PersistentEnvironmentContext<EmulatorSetup>.CreateAsync();

        TimelineRun first = await timeline.SetupRun(null, output).SetEnv(persistent.CreateEnvironment()).RunAsync();
        TimelineRun second = await timeline.SetupRun(null, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        first.EnsureRanToCompletion();
        second.EnsureRanToCompletion();

        Assert.Equal("emulator-1", first.VariableStore.GetVariable<string>("seen"));
        Assert.Equal("emulator-1", second.VariableStore.GetVariable<string>("seen"));
        Assert.Equal(1, Volatile.Read(ref starts));
    }

    [Fact]
    public async Task AReplayedAddressBeatsThePlaceholderSomebodyWroteDown()
    {
        // The failure this prevents, exactly: a configuration file cannot hold a container's port, so it holds
        // a default instead. If the carried-over address arrived as another declaration the two would race,
        // and the run would sometimes dial the default. It arrives as a produced value, which always wins.
        Interlocked.Exchange(ref starts, 0);

        SourceProvider services = new SourceProvider(new PlaceholderSource());

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        await using PersistentEnvironmentContext<EmulatorSetup> persistent =
            await PersistentEnvironmentContext<EmulatorSetup>.CreateAsync();

        TimelineRun run = await timeline.SetupRun(services, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal("emulator-1", run.VariableStore.GetVariable<string>("seen"));
    }

    [Fact]
    public async Task ABootstrapComponentReadsWhatAnEarlierOnePublished()
    {
        // A bootstrap is not a run, and it used to be handed a resolution that answered nothing. So the second
        // component's only way to learn where the first ended up was to reach for its state object and take
        // the address out of it - which is how a component ends up knowing another package's internals.
        Interlocked.Exchange(ref starts, 0);

        await using PersistentEnvironmentContext<EmulatorSetup> persistent =
            await PersistentEnvironmentContext<EmulatorSetup>.CreateAsync();

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep("test.emulator", "downstream")).Name("reads")
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        run.EnsureRanToCompletion();

        // Published by the second component, out of what it read from the first.
        Assert.Equal("downstream-of-emulator-1", run.VariableStore.GetVariable<string>("seen"));
    }

    [Fact]
    public async Task ARunSaysWhichComponentsItOwnsAndNeverTakesDownTheOthers()
    {
        // The two questions a run has to settle about every resource it touches, asked of the object that
        // already knows which components the run has. Before this the answer existed only as the stand-in's
        // empty DeconstructAsync: correct, and impossible for anything to read or assert.
        Interlocked.Exchange(ref starts, 0);
        Interlocked.Exchange(ref teardowns, 0);

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep()).Name("reads")
            .Build();

        await using PersistentEnvironmentContext<EmulatorSetup> persistent =
            await PersistentEnvironmentContext<EmulatorSetup>.CreateAsync();

        TimelineRun run = await timeline.SetupRun(null, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(["emulator", "downstream"], run.EnvironmentContext.ComponentsThisRunReuses.Select(static id => id.Identifier));
        Assert.Empty(run.EnvironmentContext.ComponentsThisRunOwns);
        Assert.Equal(EnvComponentScope.Reused, run.EnvironmentContext.ScopeOf("emulator"));

        // Still standing: the run finished and took nothing down, because it created nothing.
        Assert.Equal(0, Volatile.Read(ref teardowns));
    }

    [Fact]
    public async Task AnEnvironmentsOwnDeclarationReachesARunThroughTheWrappersAroundIt()
    {
        // A run almost never holds the environment somebody wrote: a persistent slice wraps it to hand back
        // what it already started, and a fixture wraps that again. So an environment that declares resources
        // is exactly the kind that gets wrapped, and asking only the outermost object would have made this
        // case fail silently - an environment whose declarations vanish looks like one that declares nothing.
        await using PersistentEnvironmentContext<DeclaringSetup> persistent =
            await PersistentEnvironmentContext<DeclaringSetup>.CreateAsync();

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep(Declared.Name, "named")).Name("reads")
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal("from-the-definition", run.VariableStore.GetVariable<string>("seen"));
    }

    [Fact]
    public async Task SomethingWrittenDownBeatsWhatTheEnvironmentDeclares()
    {
        // The precedence a default needs, and the opposite of the one a *published* address needs. An
        // environment declares what a resource is when nobody said - the database a definition names - so
        // somebody who did say outranks it. Where an environment does win is at run time, by publishing,
        // which answers a different question: not what this resource is, but where it ended up.
        SourceProvider services = new SourceProvider(new WrittenDownSource());

        await using PersistentEnvironmentContext<DeclaringSetup> persistent =
            await PersistentEnvironmentContext<DeclaringSetup>.CreateAsync();

        Timeline timeline = Timeline.Create()
            .Trigger(new ReadingStep(Declared.Name, "named")).Name("reads")
            .Build();

        TimelineRun run = await timeline.SetupRun(services, output).SetEnv(persistent.CreateEnvironment()).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal("written-down", run.VariableStore.GetVariable<string>("seen"));
    }

    private sealed class DeclaringSetup : IPersistentEnvironmentSetup
    {
        public IEnvironmentProvider CreateEnvironment() => new DeclaringEnvironment();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["idle"];
    }

    /// <summary>An environment that says what one of its resources is, the way a definition does.</summary>
    private sealed class DeclaringEnvironment : EnvironmentProviderBase, IResourceNodeSource
    {
        private readonly Defaults defaults = new Defaults();

        public DeclaringEnvironment() => this.AddComponent(new IdleComponent());

        public string SourceName => this.defaults.SourceName;

        public IReadOnlyList<ResourceNode> Nodes => this.defaults.Nodes;

        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
            => ["idle"];

        private sealed class Defaults : DeclaredNodeSource
        {
            public override string SourceName => "definition";

            protected override IEnumerable<DeclaredResource> Declarations =>
            [
                new DeclaredResource(
                    Declared,
                    "named",
                    new Dictionary<ValueKey, string>
                    {
                        [new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = "from-the-definition",
                    },
                    "definition"),
            ];
        }
    }

    /// <summary>Starts nothing; it exists so the environment has a persistent slice to be wrapped for.</summary>
    private sealed class IdleComponent : EnvComponent
    {
        public override EnvComponentIdentifier Id => "idle";

        public override EnvComponentReuseMode ReuseMode => EnvComponentReuseMode.PersistentContext;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
            => Task.FromResult<object?>(null);

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }

    /// <summary>The same resource, as somebody actually wrote it down.</summary>
    private sealed class WrittenDownSource : DeclaredNodeSource
    {
        public override string SourceName => "configuration";

        protected override IEnumerable<DeclaredResource> Declarations =>
        [
            new DeclaredResource(
                Declared,
                "named",
                new Dictionary<ValueKey, string>
                {
                    [new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = "written-down",
                },
                "configuration"),
        ];
    }

    private sealed class EmulatorSetup : IPersistentEnvironmentSetup
    {
        public IEnvironmentProvider CreateEnvironment() => new EmulatorEnvironment();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["downstream"];
    }

    private sealed class EmulatorEnvironment : EnvironmentProviderBase
    {
        public EmulatorEnvironment()
        {
            this.AddComponent(new EmulatorComponent());
            this.AddComponent(new DownstreamComponent());
        }

        /// <summary>
        /// Always both. A real environment resolves what the steps require; here the point is what a run sees
        /// once the components it reuses have been resolved.
        /// </summary>
        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
            => ["emulator", "downstream"];
    }

    /// <summary>Stands for a container: its address exists only because it started.</summary>
    private sealed class EmulatorComponent : EnvComponent
    {
        public override EnvComponentIdentifier Id => "emulator";

        public override EnvComponentReuseMode ReuseMode => EnvComponentReuseMode.PersistentContext;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            string address = $"emulator-{Interlocked.Increment(ref starts)}";

            PublishOn(context).Produce(Emulator, "emulator", ValueNames.ConnectionString, ResourceVantage.Host, address);

            return Task.FromResult<object?>(address);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
        {
            Interlocked.Increment(ref teardowns);

            return Task.CompletedTask;
        }
    }

    /// <summary>Stands for something configured against the emulator, and so has to read its address.</summary>
    private sealed class DownstreamComponent : EnvComponent
    {
        public override EnvComponentIdentifier Id => "downstream";

        public override EnvComponentReuseMode ReuseMode => EnvComponentReuseMode.PersistentContext;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => ["emulator"];

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            string upstream = context.Values.Require(
                ValueRef.For(Emulator.Name, "emulator", ValueNames.ConnectionString),
                ResourceVantage.Host);

            PublishOn(context).Produce(Emulator, "downstream", ValueNames.ConnectionString, ResourceVantage.Host, $"downstream-of-{upstream}");

            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
        {
            Interlocked.Increment(ref teardowns);

            return Task.CompletedTask;
        }
    }

    /// <summary>What a configuration file can say about a container: a default, and nothing truer.</summary>
    private sealed class PlaceholderSource : DeclaredNodeSource
    {
        public override string SourceName => "placeholder configuration";

        protected override IEnumerable<DeclaredResource> Declarations =>
        [
            new DeclaredResource(
                Emulator,
                "emulator",
                new Dictionary<ValueKey, string>
                {
                    [new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = "default-port",
                },
                "placeholder configuration"),
        ];
    }

    private sealed class SourceProvider(IResourceNodeSource source) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IResourceNodeSource>) ? new[] { source } : null;
    }

    private sealed class ReadingStep(string kind = "test.emulator", string identifier = "emulator") : Step<EmptyStepResultContext>
    {
        public override string Name => "Reading";

        public override string Description => "Asks the run where a reused resource ended up.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ReadingStep(kind, identifier).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable(
                "seen",
                context.Values.Require(ValueRef.For(kind, identifier, ValueNames.ConnectionString), ResourceVantage.Host));

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
