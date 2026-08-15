using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class CoreEnvironmentTests
{
    [Fact]
    public async Task SetEnv_CreatesOnlyRequiredComponents_InDependencyOrder_AndCleansUpInReverse()
    {
        TestEnvironment environment = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(
            [
                "create:network",
                "create:container",
                "deconstruct:container:state:container",
                "deconstruct:network:state:network",
            ],
            environment.Calls);
        Assert.True(run.EnvironmentContext.TryGetState<string>("network", out string? networkState));
        Assert.Equal("state:network", networkState);
        Assert.True(run.EnvironmentContext.TryGetState<string>("container", out string? containerState));
        Assert.Equal("state:container", containerState);
        Assert.False(run.EnvironmentContext.Contains("volume"));
    }

    [Fact]
    public async Task SetEnv_WithCyclicDependencies_FailsRun()
    {
        CyclicEnvironment environment = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains("cyclic environment component dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetEnv_ResolvesOpenGenericArtifactMappings()
    {
        OpenGenericEnvironment environment = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("generic", new GenericArtifactReference<string>(), new GenericArtifactData<string>())
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(
            [
                "create:generic-component",
                "deconstruct:generic-component:state:generic-component",
            ],
            environment.Calls);
    }

    [Fact]
    public async Task SetEnv_ResolvesStepLevelEnvironmentRequirements()
    {
        RequirementEnvironment environment = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new RequirementStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(
            [
                "create:servicebus-component",
                "deconstruct:servicebus-component:state:servicebus-component",
            ],
            environment.Calls);
    }

    [Fact]
    public async Task SetEnv_WithParallelCreationEnabled_CreatesDependencyReadyComponentsConcurrently()
    {
        ParallelEnvironment environment = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();

        Assert.True(environment.MaxConcurrentCreates >= 2, $"Expected at least two concurrent component creations, but observed {environment.MaxConcurrentCreates}.");

        int alphaEnd = environment.Calls.IndexOf("end:alpha");
        int betaEnd = environment.Calls.IndexOf("end:beta");
        int gammaCreate = environment.Calls.IndexOf("create:gamma");

        Assert.True(alphaEnd >= 0);
        Assert.True(betaEnd >= 0);
        Assert.True(gammaCreate > alphaEnd);
        Assert.True(gammaCreate > betaEnd);
    }

    [Fact]
    public void SetEnv_WhenCalledTwice_Throws()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        var builder = timeline.SetupRun()
            .SetEnv(new TestEnvironment());

        Action act = () => builder.SetEnv(new RequirementEnvironment());

        FrameworkConfigurationException exception = Assert.Throws<FrameworkConfigurationException>(act);

        Assert.Contains("Only one environment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersistentEnvironmentContext_ReusesPersistentComponentsAcrossRuns_AndDisposesThemOnce()
    {
        PersistentEnvironmentContext<PersistentTestSetup> persistent =
            await PersistentEnvironmentContext<PersistentTestSetup>.CreateAsync();
        try
        {
            Timeline timeline = Timeline.Create()
                .Trigger(new NoOpStep())
                .Build();

            TimelineRun firstRun = await timeline.SetupRun()
                .AddArtifact("artifact-1", new TestArtifactReference(), new TestArtifactData())
                .SetEnv(persistent.CreateEnvironment())
                .RunAsync();

            TimelineRun secondRun = await timeline.SetupRun()
                .AddArtifact("artifact-2", new TestArtifactReference(), new TestArtifactData())
                .SetEnv(persistent.CreateEnvironment())
                .RunAsync();

            firstRun.EnsureRanToCompletion();
            secondRun.EnsureRanToCompletion();
        }
        finally
        {
            await persistent.DisposeAsync();
        }

        Assert.Equal(
            [
                "create:network",
                "create:container",
                "deconstruct:container:state:container",
                "create:container",
                "deconstruct:container:state:container",
                "deconstruct:network:state:network",
            ],
            PersistentEnvironment.Calls);
    }

    [Fact]
    public async Task PersistentEnvironmentContext_WhenPersistentRootDependsOnPerRunComponent_Throws()
    {
        FrameworkConfigurationException exception = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => PersistentEnvironmentContext<InvalidPersistentSetup>.CreateAsync());

        Assert.Contains("depends on per-run component", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersistentEnvironmentContext_WhenBootstrapExceedsConfiguredTimeout_Throws()
    {
        TimeoutPersistentEnvironment.Reset();

        FrameworkTimeoutException exception = await Assert.ThrowsAsync<FrameworkTimeoutException>(
            () => PersistentEnvironmentContext<TimeoutPersistentSetup>.CreateAsync());

        Assert.Contains("configured timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delayed-network", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestEnvironment : EnvironmentProviderBase
    {
        public List<string> Calls { get; } = [];

        public TestEnvironment()
        {
            AddComponent(new LoggingEnvComponent("network", Calls));
            AddComponent(new LoggingEnvComponent("container", Calls, ["network"]));
            AddComponent(new LoggingEnvComponent("volume", Calls));
            MapArtifact<TestArtifactDescriber>("container");
        }
    }

    private sealed class CyclicEnvironment : EnvironmentProviderBase
    {
        public CyclicEnvironment()
        {
            AddComponent(new LoggingEnvComponent("alpha", [], ["beta"]));
            AddComponent(new LoggingEnvComponent("beta", [], ["alpha"]));
            MapArtifact<TestArtifactDescriber>("alpha");
        }
    }

    private sealed class OpenGenericEnvironment : EnvironmentProviderBase
    {
        public List<string> Calls { get; } = [];

        public OpenGenericEnvironment()
        {
            AddComponent(new LoggingEnvComponent("generic-component", Calls));
            MapArtifact(typeof(GenericArtifactDescriber<>), "generic-component");
        }
    }

    private sealed class RequirementEnvironment : EnvironmentProviderBase
    {
        public List<string> Calls { get; } = [];

        public RequirementEnvironment()
        {
            AddComponent(new LoggingEnvComponent("servicebus-component", Calls));
            MapResourceKind("test.servicebus", "servicebus-component");
        }
    }

    private sealed class ParallelEnvironment : EnvironmentProviderBase
    {
        public List<string> Calls { get; } = [];
        private int _activeCreates;

        public override bool SupportsParallelComponentCreation => true;

        public int MaxConcurrentCreates { get; private set; }

        public ParallelEnvironment()
        {
            AddComponent(new DelayedLoggingEnvComponent("alpha", this, Calls));
            AddComponent(new DelayedLoggingEnvComponent("beta", this, Calls));
            AddComponent(new LoggingEnvComponent("gamma", Calls, ["alpha", "beta"]));
            MapArtifact<TestArtifactDescriber>("gamma");
        }

        public void OnCreateStart()
        {
            int activeCreates = Interlocked.Increment(ref _activeCreates);
            MaxConcurrentCreates = Math.Max(MaxConcurrentCreates, activeCreates);
        }

        public void OnCreateEnd()
        {
            Interlocked.Decrement(ref _activeCreates);
        }
    }

    private sealed class PersistentEnvironment : EnvironmentProviderBase
    {
        public static List<string> Calls { get; } = [];

        public PersistentEnvironment()
        {
            AddComponent(new LoggingEnvComponent("network", Calls) { ReuseModeOverride = EnvComponentReuseMode.PersistentContext });
            AddComponent(new LoggingEnvComponent("container", Calls, ["network"]));
            MapArtifact<TestArtifactDescriber>("container");
        }

        public static void Reset() => Calls.Clear();
    }

    private sealed class InvalidPersistentEnvironment : EnvironmentProviderBase
    {
        public InvalidPersistentEnvironment()
        {
            AddComponent(new LoggingEnvComponent("token-provider", []));
            AddComponent(new LoggingEnvComponent("shared-host", [], ["token-provider"]) { ReuseModeOverride = EnvComponentReuseMode.PersistentContext });
        }
    }

    private sealed class PersistentTestSetup : IPersistentEnvironmentSetup
    {
        public PersistentTestSetup()
        {
            PersistentEnvironment.Reset();
        }

        public IEnvironmentProvider CreateEnvironment() => new PersistentEnvironment();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["network"];
    }

    private sealed class InvalidPersistentSetup : IPersistentEnvironmentSetup
    {
        public IEnvironmentProvider CreateEnvironment() => new InvalidPersistentEnvironment();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["shared-host"];
    }

    private sealed class TimeoutPersistentEnvironment : EnvironmentProviderBase
    {
        public static List<string> Calls { get; } = [];

        public TimeoutPersistentEnvironment()
        {
            AddComponent(new TimeoutLoggingEnvComponent("delayed-network", Calls) { ReuseModeOverride = EnvComponentReuseMode.PersistentContext });
        }

        public static void Reset() => Calls.Clear();
    }

    private sealed class TimeoutPersistentSetup : IPersistentEnvironmentSetup
    {
        public IEnvironmentProvider CreateEnvironment() => new TimeoutPersistentEnvironment();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["delayed-network"];

        public TimeSpan GetPersistentSetupTimeout() => TimeSpan.FromMilliseconds(50);
    }

    private sealed class LoggingEnvComponent(string identifier, List<string> calls, params EnvComponentIdentifier[] dependencies) : EnvComponent
    {
        private readonly IReadOnlyList<EnvComponentIdentifier> _dependencies = dependencies;

        public override EnvComponentIdentifier Id => identifier;

        public EnvComponentReuseMode ReuseModeOverride { get; init; } = EnvComponentReuseMode.PerRun;

        public override EnvComponentReuseMode ReuseMode => ReuseModeOverride;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => _dependencies;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            calls.Add($"create:{Id}");
            return Task.FromResult((object?)$"state:{Id}");
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            calls.Add($"deconstruct:{Id}:{state}");
            return Task.CompletedTask;
        }
    }

    private sealed class TimeoutLoggingEnvComponent(string identifier, List<string> calls, params EnvComponentIdentifier[] dependencies) : EnvComponent
    {
        private readonly IReadOnlyList<EnvComponentIdentifier> _dependencies = dependencies;

        public override EnvComponentIdentifier Id => identifier;

        public EnvComponentReuseMode ReuseModeOverride { get; init; } = EnvComponentReuseMode.PerRun;

        public override EnvComponentReuseMode ReuseMode => ReuseModeOverride;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => _dependencies;

        public override async Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            calls.Add($"create:{Id}:start");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            calls.Add($"create:{Id}:end");
            return $"state:{Id}";
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class DelayedLoggingEnvComponent(string identifier, ParallelEnvironment environment, List<string> calls, params EnvComponentIdentifier[] dependencies) : EnvComponent
    {
        private readonly IReadOnlyList<EnvComponentIdentifier> _dependencies = dependencies;

        public override EnvComponentIdentifier Id => identifier;

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => _dependencies;

        public override async Task<object?> CreateAsync(IEnvironmentProvider environmentProvider, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            calls.Add($"start:{Id}");
            environment.OnCreateStart();
            try
            {
                await Task.Delay(75, cancellationToken);
                calls.Add($"end:{Id}");
                calls.Add($"create:{Id}");
                return $"state:{Id}";
            }
            finally
            {
                environment.OnCreateEnd();
            }
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environmentProvider, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            calls.Add($"deconstruct:{Id}:{state}");
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpStep : Step<EmptyStepResultContext>
    {
        public override string Name => "NoOp";
        public override string Description => "NoOp";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new NoOpStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class RequirementStep : Step<EmptyStepResultContext>, IHasEnvironmentRequirements
    {
        public override string Name => "Requirement";
        public override string Description => "Requirement";
        public override bool DoesReturn => false;

        public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
            => [new EnvironmentRequirement("test.servicebus", "bus")];

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new RequirementStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class TestArtifactDescriber : ArtifactDescriber<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, TestArtifactData data, TestArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, TestArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => nameof(TestArtifactDescriber);
    }

    private sealed class TestArtifactData : ArtifactData<TestArtifactData, TestArtifactDescriber, TestArtifactReference>
    {
        public override string ToString() => nameof(TestArtifactData);
    }

    private sealed class TestArtifactReference : ArtifactReference<TestArtifactReference, TestArtifactDescriber, TestArtifactData>
    {
        public override Task<ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
            {
                Found = true,
                Data = new TestArtifactData(),
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => nameof(TestArtifactReference);
    }

    private sealed class GenericArtifactDescriber<T> : ArtifactDescriber<GenericArtifactDescriber<T>, GenericArtifactData<T>, GenericArtifactReference<T>>
    {
        public override Task Setup(IServiceProvider serviceProvider, GenericArtifactData<T> data, GenericArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, GenericArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => nameof(GenericArtifactDescriber<T>);
    }

    private sealed class GenericArtifactData<T> : ArtifactData<GenericArtifactData<T>, GenericArtifactDescriber<T>, GenericArtifactReference<T>>
    {
        public override string ToString() => nameof(GenericArtifactData<T>);
    }

    private sealed class GenericArtifactReference<T> : ArtifactReference<GenericArtifactReference<T>, GenericArtifactDescriber<T>, GenericArtifactData<T>>
    {
        public override Task<ArtifactResolveResult<GenericArtifactDescriber<T>, GenericArtifactData<T>, GenericArtifactReference<T>>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<GenericArtifactDescriber<T>, GenericArtifactData<T>, GenericArtifactReference<T>>
            {
                Found = true,
                Data = new GenericArtifactData<T>(),
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => nameof(GenericArtifactReference<T>);
    }
}