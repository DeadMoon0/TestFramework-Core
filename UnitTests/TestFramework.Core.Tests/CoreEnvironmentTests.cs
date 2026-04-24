using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    public void SetEnv_WhenCalledTwice_Throws()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NoOpStep())
            .Build();

        var builder = timeline.SetupRun()
            .SetEnv(new TestEnvironment());

        Action act = () => builder.SetEnv(new RequirementEnvironment());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(act);

        Assert.Contains("Only one environment", exception.Message, StringComparison.Ordinal);
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

    private sealed class LoggingEnvComponent(string identifier, List<string> calls, params EnvComponentIdentifier[] dependencies) : EnvComponent
    {
        private readonly IReadOnlyList<EnvComponentIdentifier> _dependencies = dependencies;

        public override EnvComponentIdentifier Id => identifier;

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

    private sealed class NoOpStep : Step<object?>
    {
        public override string Name => "NoOp";
        public override string Description => "NoOp";
        public override bool DoesReturn => false;

        public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult((object?)null);

        public override Step<object?> Clone() => new NoOpStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);
    }

    private sealed class RequirementStep : Step<object?>, IHasEnvironmentRequirements
    {
        public override string Name => "Requirement";
        public override string Description => "Requirement";
        public override bool DoesReturn => false;

        public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
            => [new EnvironmentRequirement("test.servicebus", "bus")];

        public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult((object?)null);

        public override Step<object?> Clone() => new RequirementStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);
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