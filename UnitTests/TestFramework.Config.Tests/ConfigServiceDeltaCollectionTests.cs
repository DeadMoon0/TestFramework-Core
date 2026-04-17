using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;

namespace TestFramework.Config.Tests;

public class ConfigServiceDeltaCollectionTests
{
    [Fact]
    public void ApplyConfigDeltas_InheritsBaseValuesAndAppliesOverrides()
    {
        ConfigServiceDeltaCollection parent = new(null);
        parent.AddConfigDelta("App:Mode", "base");
        parent.AddConfigDelta("App:Region", "eu");

        ConfigServiceDeltaCollection child = new(parent);
        child.AddConfigDelta("App:Mode", "child");

        Dictionary<string, string?> values = child.ApplyConfigDeltas();

        Assert.Equal("child", values["App:Mode"]);
        Assert.Equal("eu", values["App:Region"]);
    }

    [Fact]
    public void ApplyServiceDeltas_ResolvesBaseAndChildRegistrationsAgainstMergedConfiguration()
    {
        ConfigServiceDeltaCollection parent = new(null);
        parent.AddConfigDelta("App:Mode", "base");
        parent.AddServiceDelta((services, configuration) =>
            services.AddSingleton(new MarkerService("parent", configuration["App:Mode"] ?? string.Empty)));

        ConfigServiceDeltaCollection child = new(parent);
        child.AddConfigDelta("App:Mode", "child");
        child.AddServiceDelta((services, configuration) =>
            services.AddSingleton(new MarkerService("child", configuration["App:Mode"] ?? string.Empty)));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(child.ApplyConfigDeltas())
            .Build();

        ServiceCollection services = child.ApplyServiceDeltas().Resolve(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        MarkerService[] markers = provider.GetServices<MarkerService>().OrderBy(x => x.Source).ToArray();

        Assert.Collection(
            markers,
            marker =>
            {
                Assert.Equal("child", marker.Source);
                Assert.Equal("child", marker.Mode);
            },
            marker =>
            {
                Assert.Equal("parent", marker.Source);
                Assert.Equal("child", marker.Mode);
            });
    }

    private sealed record MarkerService(string Source, string Mode);
}