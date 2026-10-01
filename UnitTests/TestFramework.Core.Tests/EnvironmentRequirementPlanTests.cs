using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// One rule, checked before the first step: every requirement names a resource the run declares. And an
/// environment is handed exactly the requirements that are its to provide.
/// </summary>
public class EnvironmentRequirementPlanTests
{
    private static readonly ResourceKind ApiKind = ResourceKind.Named("test.plan.api").Build();
    private static readonly ResourceKind SiteKind = ResourceKind.Named("test.plan.site").Build();

    [Fact]
    public async Task ARequirementNothingDeclares_IsRefusedBeforeAnyStepRuns_NamingWhatExists()
    {
        // Damage without it: the run started, and the step failed later against an address nobody supplied.
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(ApiKind.Requirement("billing"))).Name("pay")
            .Build();

        FrameworkConfigurationException refusal = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline.SetupRun(Declaring(("orders", ApiKind))).RunAsync());

        Assert.Contains("Step 'pay' requires test.plan.api 'billing'", refusal.Message);
        Assert.Contains("test.plan.api 'orders' (test declarations)", refusal.AvailableOptions);
    }

    [Fact]
    public async Task ARunWithoutAnEnvironment_IsLegal_WhenWhatItNeedsIsDeclared()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(ApiKind.Requirement("orders")))
            .Build();

        TimelineRun run = await timeline.SetupRun(Declaring(("orders", ApiKind))).RunAsync();

        run.EnsureRanToCompletion();
    }

    [Fact]
    public async Task AnEnvironment_ReceivesTheRequirementsForWhatItDeclares_AndNotWhatConfigurationDeclares()
    {
        // Damage without routing: an environment that runs one container per declared API received every
        // API requirement, and refused the one configuration pointed at a deployed system.
        DefinitionEnvironment environment = new(("orders", ApiKind));
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(ApiKind.Requirement("orders"), ApiKind.Requirement("deployed")))
            .Build();

        TimelineRun run = await timeline.SetupRun(Declaring(("deployed", ApiKind))).SetEnv(environment).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal([ApiKind.Requirement("orders")], environment.Required);
    }

    [Fact]
    public async Task AnEnvironmentStandingInForAWholeKind_ReceivesEveryRequirementOfIt()
    {
        EmulatorEnvironment environment = new(ApiKind);
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(ApiKind.Requirement("deployed")))
            .Build();

        TimelineRun run = await timeline.SetupRun(Declaring(("deployed", ApiKind))).SetEnv(environment).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal([ApiKind.Requirement("deployed")], environment.Required);
    }

    [Fact]
    public async Task AKindAgnosticRequirement_ArrivesWithTheKindItResolvedTo()
    {
        DefinitionEnvironment environment = new(("storefront", SiteKind));
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(EnvironmentRequirement.AnyKind("storefront")))
            .Build();

        TimelineRun run = await timeline.SetupRun().SetEnv(environment).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal([SiteKind.Requirement("storefront")], environment.Required);
    }

    [Fact]
    public async Task AKindAgnosticRequirement_ThatTwoKindsAnswer_IsRefusedNamingBoth()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(EnvironmentRequirement.AnyKind("shop"))).Name("open")
            .Build();

        FrameworkConfigurationException refusal = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline.SetupRun(Declaring(("shop", ApiKind), ("shop", SiteKind))).RunAsync());

        Assert.Contains("2 kinds of resource carry that name", refusal.Message);
        Assert.Equal(2, refusal.AvailableOptions.Count);
    }

    [Fact]
    public async Task ARequirementDecidedFromTheDeclaredResources_IsWhatTheEngineChecks()
    {
        // A piece that owns a naming convention decides it with the run's resources in view: here, "the
        // one site whose name matches", which the old overload could not see.
        Timeline timeline = Timeline.Create()
            .Trigger(new ChoosesSiteStep("storefront"))
            .Build();

        TimelineRun run = await timeline.SetupRun(Declaring(("storefront", SiteKind))).RunAsync();

        run.EnsureRanToCompletion();
    }

    [Fact]
    public async Task AnEnvironmentThatDoesNotAnswer_KeepsReceivingEveryRequirement_AsItAlwaysDid()
    {
        // An environment written against an earlier engine implements neither declaration nor the new
        // question; the default keeps its behaviour exactly as it was.
        PlainEnvironment environment = new();
        Timeline timeline = Timeline.Create()
            .Trigger(new NeedsStep(ApiKind.Requirement("deployed")))
            .Build();

        TimelineRun run = await timeline.SetupRun(Declaring(("deployed", ApiKind))).SetEnv(environment).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal([ApiKind.Requirement("deployed")], environment.Resolved);
    }

    private static IServiceProvider Declaring(params (string Identifier, ResourceKind Kind)[] resources)
    {
        return new SourceProvider(new TestDeclarations("test declarations", resources));
    }

    /// <summary>
    /// Hands the run one declaration source, the way a configuration loader registers one, and nothing else.
    /// </summary>
    private sealed class SourceProvider(IResourceNodeSource source) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IResourceNodeSource>) ? new[] { source } : null;
    }

    private sealed class TestDeclarations(string sourceName, (string Identifier, ResourceKind Kind)[] resources) : DeclaredNodeSource
    {
        public override string SourceName => sourceName;

        protected override IEnumerable<DeclaredResource> Declarations
            => resources.Select(resource => new DeclaredResource { Kind = resource.Kind, Identifier = resource.Identifier, Values = new Dictionary<ValueKey, string>(), DeclaredBy = sourceName });
    }

    /// <summary>
    /// Runs one container per resource it declares - and records what its component was handed.
    /// </summary>
    private sealed class DefinitionEnvironment : EnvironmentProviderBase, IResourceNodeSource
    {
        private readonly TestDeclarations _declarations;

        public DefinitionEnvironment(params (string Identifier, ResourceKind Kind)[] resources)
        {
            this._declarations = new TestDeclarations("definition environment", resources);
            RecordingComponent component = new(this.Required);
            this.AddComponent(component);
            foreach (ResourceKind kind in resources.Select(static resource => resource.Kind).Distinct())
            {
                this.MapDeclaredResources(kind.Name, component.Id);
            }
        }

        public List<EnvironmentRequirement> Required { get; } = [];

        public string SourceName => this._declarations.SourceName;

        public IReadOnlyList<ResourceNode> Nodes => this._declarations.Nodes;
    }

    /// <summary>
    /// Stands in for every resource of one kind, declaring none of them.
    /// </summary>
    private sealed class EmulatorEnvironment : EnvironmentProviderBase
    {
        public EmulatorEnvironment(ResourceKind kind)
        {
            RecordingComponent component = new(this.Required);
            this.AddComponent(component);
            this.MapResourceKind(kind.Name, component.Id);
        }

        public List<EnvironmentRequirement> Required { get; } = [];
    }

    /// <summary>
    /// Implements the provider interface directly, the way an environment built against an earlier engine does.
    /// </summary>
    private sealed class PlainEnvironment : IEnvironmentProvider
    {
        public List<EnvironmentRequirement> Resolved { get; } = [];

        public IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<Artifacts.ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements)
        {
            this.Resolved.AddRange(requirements);
            return [];
        }

        public EnvComponent GetComponent(EnvComponentIdentifier identifier)
            => throw new KeyNotFoundException(identifier.ToString());
    }

    private sealed class RecordingComponent(List<EnvironmentRequirement> into) : EnvComponent
    {
        public override EnvComponentIdentifier Id => new("recording");

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            into.AddRange(PublishOn(context).Required);
            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
    }

    private class NeedsStep(params EnvironmentRequirement[] requirements) : Step<EmptyStepResultContext>, IHasEnvironmentRequirements
    {
        public override string Name => "Needs";
        public override string Description => "Needs resources.";
        public override bool DoesReturn => false;

        public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore) => requirements;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new NeedsStep(requirements).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class ChoosesSiteStep(string name) : Step<EmptyStepResultContext>, IHasEnvironmentRequirements
    {
        public override string Name => "Chooses";
        public override string Description => "Chooses its site from what the run declares.";
        public override bool DoesReturn => false;

        // The old overload cannot see the resources; required only because the interface still declares it.
        public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
            => [EnvironmentRequirement.AnyKind("not-" + name)];

        public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore, ResourceGraph resources)
            => [.. resources.NodesNamed(name).Where(static node => node.KindName == "test.plan.site").Select(static node => new EnvironmentRequirement(node.KindName, node.Identifier))];

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new ChoosesSiteStep(name).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
