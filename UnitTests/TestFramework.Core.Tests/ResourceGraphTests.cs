using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The graph: what a run has, what needs what, what gets provisioned and in which order - all decided
/// before anything starts.
/// </summary>
public class ResourceGraphTests
{
    // Declared the way a package declares its kinds: once, with the values every instance may offer.
    private static readonly ResourceKind Api = ResourceKind.Named("web.restapi")
        .OffersPerVantage(ValueNames.BaseUrl)
        .Offers("HealthPath", optional: true);

    private static readonly ResourceKind Sql = ResourceKind.Named("web.sql")
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers("DatabaseName");

    private static readonly ResourceKind Stub = ResourceKind.Named("web.stub")
        .OffersPerVantage(ValueNames.BaseUrl);

    private static readonly ResourceKind Site = ResourceKind.Named("web.site")
        .OffersPerVantage(ValueNames.BaseUrl);

    [Fact]
    public void ALaterEnvironmentShadowsAnEarlierOnePerNode()
    {
        // The one precedence rule: config relays first, containers stack on top. Neither source knows
        // about the other.
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("config", [new RelayNode(Api, "orders-api")]),
            new TestSource("DockerWebEnvironment", [new StartedNode(Api, "orders-api")]),
        ]);

        Assert.True(graph.TryGetNode(Api.Name, "orders-api", out ResourceNode? node));
        Assert.IsType<StartedNode>(node);
        Assert.Equal("DockerWebEnvironment", graph.ProviderOf(node!));
    }

    [Fact]
    public void OnlyWhatTheRunReachesIsProvisioned()
    {
        // A solution declares everything it has; one test touches a corner of it. Reachability follows
        // connections, so a neighbour comes along without being asked for by name.
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("env",
            [
                new StartedNode(Api, "orders-api", ordering: [new ResourceAddress(Sql.Name, "orders-db")]),
                new StartedNode(Sql, "orders-db"),
                new StartedNode(Stub, "payments"),
            ]),
        ]);

        IReadOnlyList<ResourceNode> reachable = graph.Reachable([new EnvironmentRequirement(Api.Name, "orders-api")]);

        Assert.Equal(["web.restapi/orders-api", "web.sql/orders-db"], reachable.Select(static node => node.ToString()).OrderBy(static name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void AKindAgnosticRequirementIsAnsweredByTheGraph()
    {
        // The browser says "storefront" and never learns whether a site or an API served it.
        ResourceGraph graph = ResourceGraph.Compose([new TestSource("env", [new StartedNode(Site, "storefront")])]);

        IReadOnlyList<ResourceNode> reachable = graph.Reachable([EnvironmentRequirement.AnyKind("storefront")]);

        Assert.Equal("web.site/storefront", Assert.Single(reachable).ToString());
    }

    [Fact]
    public void CreationOrderPutsEveryNodeAfterWhatItConnectsTo()
    {
        // Derived from the connections, so the ordering cannot drift away from the wiring the way a
        // hand-listed dependency set could.
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("env",
            [
                new StartedNode(Api, "orders-api", ordering: [new ResourceAddress(Sql.Name, "orders-db"), new ResourceAddress(Stub.Name, "payments")]),
                new StartedNode(Sql, "orders-db"),
                new StartedNode(Stub, "payments"),
            ]),
        ]);

        IReadOnlyList<ResourceNode> order = graph.CreationOrder(graph.Nodes);
        List<string> names = [.. order.Select(static node => node.ToString())];

        Assert.Equal("web.restapi/orders-api", names[^1]);
        Assert.Contains("web.sql/orders-db", names);
        Assert.Contains("web.stub/payments", names);
    }

    [Fact]
    public void AMissingNeighbourFailsBeforeAnythingStarts()
    {
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("DockerWebEnvironment",
            [
                new StartedNode(Api, "orders-api",
                    routes: [ValueRoute.To(StubBaseUrl.Of("payments"), "appsettings.json", "Services:Payments:BaseUrl")]),
                new StartedNode(Sql, "orders-db"),
            ]),
        ]);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(graph.Validate);

        Assert.Contains("Nothing provides web.stub/payments, needed by web.restapi/orders-api", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Services:Payments:BaseUrl", failure.Message, StringComparison.Ordinal);

        // And it says what the run does have, so the fix is a copy rather than an expedition.
        Assert.Contains("web.sql/orders-db from DockerWebEnvironment", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutingAValueAResourceDoesNotOfferFailsBeforeAnythingStarts()
    {
        // The other half of plan-time safety: the neighbour exists, but a database has no address. This
        // is the mistake a typed accessor stops at compile time - checked here for the case where a
        // route was built dynamically.
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("DockerWebEnvironment",
            [
                // A route built from a hand-made ref - the dynamic case plan-time validation exists for.
                // It has to be hand-made precisely because the Sql kind has no BaseUrl to point at.
                new StartedNode(Api, "orders-api",
                    routes: [ValueRoute.To(ValueRef.For(Sql.Name, "orders-db", ValueNames.BaseUrl), "appsettings.json", "Services:Db:BaseUrl")]),
                new StartedNode(Sql, "orders-db"),
            ]),
        ]);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(graph.Validate);

        Assert.Contains("routes BaseUrl (Network) from web.sql/orders-db, which does not offer it", failure.Message, StringComparison.Ordinal);
        Assert.Contains("web.sql offers ConnectionString", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeCannotProduceWhatItNeverOffered()
    {
        // Otherwise plan-time validation would be a promise the run does not keep.
        ResourceValueStore values = new ResourceValueStore();
        StartedNode node = new StartedNode(Sql, "orders-db");

        NodeContext context = new NodeContext(
            node,
            new ConnectionSet(node.ToString(), [], values, static (_, _) => null),
            values,
            new EmptyServices(),
            new ScopedLogger(null));

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => context.Produce(ValueNames.BaseUrl, ResourceVantage.Host, "http://localhost:1/"));

        Assert.Contains("does not offer", failure.Message, StringComparison.Ordinal);
        Assert.Contains("web.sql:ConnectionString", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACircleIsNamedRatherThanHung()
    {
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("env",
            [
                new StartedNode(Api, "first", ordering: [new ResourceAddress(Api.Name, "second")]),
                new StartedNode(Api, "second", ordering: [new ResourceAddress(Api.Name, "first")]),
            ]),
        ]);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(graph.Validate);

        Assert.Contains("depend on each other in a circle", failure.Message, StringComparison.Ordinal);
        Assert.Contains("web.restapi/first", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneNameOnTwoKindsIsStatedWhenAskedWithoutOne()
    {
        ResourceGraph graph = ResourceGraph.Compose(
        [
            new TestSource("env", [new StartedNode(Site, "storefront"), new StartedNode(Api, "storefront")]),
        ]);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => graph.TryGetNode("storefront", out _));

        Assert.Contains("names 2 different kinds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeCannotReachWhatItNeverDeclared()
    {
        // The honesty rule: an undeclared read is a dependency the graph cannot order or validate.
        ResourceValueStore values = new ResourceValueStore();
        values.Produce(Sql.Name, "orders-db", new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network), "Server=orders-db", "env");

        ConnectionSet connections = new ConnectionSet(
            "web.restapi/orders-api",
            [new Connection(Stub.Name, "payments", [])],
            values,
            static (_, _) => null);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => connections.Require(SqlConnectionString.Of("orders-db"), ResourceVantage.Network));

        Assert.Contains("without declaring a connection to it", failure.Message, StringComparison.Ordinal);
        Assert.Contains("web.stub/payments", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredNeighbourThatNeverSuppliedTheValueSaysWhatItDidSupply()
    {
        ResourceValueStore values = new ResourceValueStore();
        values.Produce(Sql.Name, "orders-db", new ValueKey("DatabaseName"), "orders", "env");

        ConnectionSet connections = new ConnectionSet(
            "web.restapi/orders-api",
            [new Connection(Sql.Name, "orders-db", [])],
            values,
            static (_, _) => null);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => connections.Require(SqlConnectionString.Of("orders-db"), ResourceVantage.Network));

        Assert.Contains("which never supplied it", failure.Message, StringComparison.Ordinal);
        Assert.Contains("DatabaseName", failure.Message, StringComparison.Ordinal);
    }

    // What a package's typed accessor delegates to - the schema entry, not a parallel truth.
    private static ResourceValue SqlConnectionString => Value(Sql, ValueNames.ConnectionString);

    private static ResourceValue StubBaseUrl => Value(Stub, ValueNames.BaseUrl);

    private static ResourceValue Value(ResourceKind kind, string valueName)
        => kind.TryGetValue(valueName, out ResourceValue? value) ? value! : throw new InvalidOperationException(valueName);

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestSource(string sourceName, IReadOnlyList<ResourceNode> nodes) : IResourceNodeSource
    {
        public string SourceName => sourceName;

        public IReadOnlyList<ResourceNode> Nodes => nodes;
    }

    /// <summary>A relayed configuration entry: no lifecycle, values as written.</summary>
    private sealed class RelayNode(ResourceKind kind, string identifier) : ResourceNode
    {
        public override ResourceKind Kind => kind;

        public override string Identifier => identifier;
    }

    /// <summary>Something the run would start.</summary>
    private sealed class StartedNode(
        ResourceKind kind,
        string identifier,
        IReadOnlyList<ValueRoute>? routes = null,
        IReadOnlyList<ResourceAddress>? ordering = null)
        : ProvisionedResourceNode
    {
        public override ResourceKind Kind => kind;

        public override string Identifier => identifier;

        public override IReadOnlyList<ValueRoute> Routes => routes ?? [];

        public override IReadOnlyList<ResourceAddress> Ordering => ordering ?? [];

        public override Task<object?> CreateAsync(NodeContext context, CancellationToken cancellationToken)
            => Task.FromResult<object?>(null);

        public override Task DeconstructAsync(object? state, NodeContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
