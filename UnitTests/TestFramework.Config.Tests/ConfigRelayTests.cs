using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Config.Tests;

/// <summary>
/// Declared configuration, relayed into the run's graph: what a store will and will not accept, and what
/// a shape may claim about the kind it describes.
/// </summary>
public class ConfigRelayTests
{
    private static readonly ResourceKind ApiKind = ResourceKind.Named("relay.tests.api")
        .OffersPerVantage(ValueNames.BaseUrl)
        .Offers("HealthPath", optional: true);

    [Fact]
    public void DeclaredEntriesBecomeResourcesTheGraphCanAnswerFor()
    {
        // The whole point of the relay: a run that provisions nothing still has resources, so a consumer
        // asks the same question it would ask of a container.
        ResourceGraph graph = ResourceGraph.Compose([Relay(
            ("Api:orders:BaseUrl", "https://orders.test/"),
            ("Api:orders:HealthPath", "/healthz"),
            ("Api:legacy:BaseUrl", "https://legacy.test/"))]);

        Assert.Equal(
            ["relay.tests.api/legacy", "relay.tests.api/orders"],
            graph.Nodes.Select(static node => node.ToString()));

        // Values as written - the relay edits nothing.
        Assert.True(graph.TryGetNode(ApiKind.Name, "orders", out ResourceNode? orders));
        Assert.Equal("https://orders.test/", orders!.DeclaredValues[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)]);
        Assert.Equal("/healthz", orders.DeclaredValues[new ValueKey("HealthPath")]);
    }

    [Fact]
    public void AnEntryThatOmitsAnOptionalValueIsANormalEntry()
    {
        // A schema is an upper bound, not a demand: an API without a health path is an ordinary API.
        ResourceGraph graph = ResourceGraph.Compose([Relay(("Api:orders:BaseUrl", "https://orders.test/"))]);

        Assert.True(graph.TryGetNode(ApiKind.Name, "orders", out ResourceNode? orders));
        Assert.DoesNotContain(new ValueKey("HealthPath"), orders!.DeclaredValues.Keys);
    }

    [Fact]
    public void ARelayedResourceHasNoLifecycle()
    {
        // Which is what keeps a configuration-only run as cheap as it always was: nothing to create.
        ResourceGraph graph = ResourceGraph.Compose([Relay(("Api:orders:BaseUrl", "https://orders.test/"))]);

        Assert.All(graph.Nodes, static node => Assert.IsNotAssignableFrom<ProvisionedResourceNode>(node));
    }

    [Fact]
    public void AShapeCannotClaimAValueItsKindDoesNotOffer()
    {
        // The same drift the kind schema prevents in code, arriving from the configuration side.
        IServiceProvider services = Services(new WrongShape(), ("Api:orders:BaseUrl", "https://orders.test/"));

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => _ = ConfigEnvironment.From(services).Nodes);

        Assert.Contains("does not offer", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionString", failure.Message, StringComparison.Ordinal);
        Assert.Contains("offers BaseUrl", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoreHoldsWhatWasDeclaredAndRefusesWhatArrivesLater()
    {
        // Declared has to keep meaning declared, or nothing can be trusted to describe intent.
        IServiceProvider services = Services(new ApiShape(), ("Api:orders:BaseUrl", "https://orders.test/"));
        ConfigStore<ApiEntry> store = services.GetRequiredService<ConfigStore<ApiEntry>>();

        Assert.Equal("https://orders.test/", store.Get("orders").BaseUrl);

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => store.Add("late", new ApiEntry { BaseUrl = "https://late.test/" }));

        Assert.Contains("arrived after loading finished", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingEntryNamesTheOnesThatExist()
    {
        IServiceProvider services = Services(new ApiShape(), ("Api:orders:BaseUrl", "https://orders.test/"));
        ConfigStore<ApiEntry> store = services.GetRequiredService<ConfigStore<ApiEntry>>();

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(() => store.Get("nope"));

        Assert.Contains("Nothing is configured as ApiEntry under 'nope'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("orders", failure.Message, StringComparison.Ordinal);

        // And it says the entry may simply be the environment's job - the zero-config case.
        Assert.Contains("leave the entry out", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsentSectionIsNoResourcesRatherThanAFailure()
    {
        // A run may configure nothing at all and still be a valid run.
        ResourceGraph graph = ResourceGraph.Compose([Relay()]);

        Assert.Empty(graph.Nodes);
    }

    private static ConfigEnvironment Relay(params (string Key, string Value)[] settings)
        => ConfigEnvironment.From(Services(new ApiShape(), settings));

    private static IServiceProvider Services(IConfigShape shape, params (string Key, string Value)[] settings)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(static setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(shape);

        if (shape is ApiShape)
        {
            services.AddConfigShape<ApiShape, ApiEntry>();
        }

        return services.BuildServiceProvider();
    }

    private sealed record ApiEntry
    {
        public string? BaseUrl { get; init; }

        public string? HealthPath { get; init; }
    }

    private sealed class ApiShape : ConfigShape<ApiEntry>
    {
        public override string Section => "Api";

        public override ResourceKind Kind => ApiKind;

        public override ApiEntry Read(IConfiguration configuration, string identifier)
        {
            IConfigurationSection section = configuration.GetSection(this.Section).GetSection(identifier);

            return new ApiEntry
            {
                BaseUrl = section[nameof(ApiEntry.BaseUrl)],
                HealthPath = section[nameof(ApiEntry.HealthPath)],
            };
        }

        public override IReadOnlyDictionary<ValueKey, string> Values(ApiEntry config)
        {
            Dictionary<ValueKey, string> values = [];

            // Only what the entry actually holds - a written address is the same from every viewpoint,
            // so it answers both.
            if (config.BaseUrl is { Length: > 0 } baseUrl)
            {
                values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)] = baseUrl;
                values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Network)] = baseUrl;
            }

            if (config.HealthPath is { Length: > 0 } healthPath)
            {
                values[new ValueKey("HealthPath")] = healthPath;
            }

            return values;
        }
    }

    /// <summary>A shape that claims a value its kind never offered.</summary>
    private sealed class WrongShape : ConfigShape<ApiEntry>
    {
        public override string Section => "Api";

        public override ResourceKind Kind => ApiKind;

        public override ApiEntry Read(IConfiguration configuration, string identifier)
        {
            IConfigurationSection section = configuration.GetSection(this.Section).GetSection(identifier);

            return new ApiEntry
            {
                BaseUrl = section[nameof(ApiEntry.BaseUrl)],
                HealthPath = section[nameof(ApiEntry.HealthPath)],
            };
        }

        public override IReadOnlyDictionary<ValueKey, string> Values(ApiEntry config)
            => new Dictionary<ValueKey, string>
            {
                [new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = "Server=nope",
            };
    }
}
