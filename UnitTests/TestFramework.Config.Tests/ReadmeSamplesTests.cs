using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;
using TestFramework.Core.Timelines;

namespace TestFramework.Config.Tests;

// README sync note: these tests mirror the public README samples for TestFramework.Config.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    [Fact]
    public void QuickStart_BuildsServiceProviderWithOverridesAndRegistrations()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"config-readme-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, """
                {
                  "FeatureFlags": {
                    "UseMockService": "false"
                  }
                }
                """);

            IServiceProvider serviceProvider = ConfigInstance
                .FromJsonFile(tempFile)
                .OverrideConfig("FeatureFlags:UseMockService", "true")
                .AddService((services, configuration) =>
                {
                    services.AddSingleton(new ReadmeDependency(configuration["FeatureFlags:UseMockService"] ?? string.Empty));
                })
                .BuildServiceProvider();

            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            ReadmeDependency dependency = serviceProvider.GetRequiredService<ReadmeDependency>();

            Assert.Equal("true", configuration["FeatureFlags:UseMockService"]);
            Assert.Equal("true", dependency.Mode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TypicalPattern_BuildsIndependentSubInstancesFromSharedConfig()
    {
        ConfigInstance shared = ConfigInstance
            .Create()
            .OverrideConfig("Run:Tenant", "base")
            .Build();

        IServiceProvider providerA = shared
            .SetupSubInstance()
            .OverrideConfig("Run:Tenant", "A")
            .BuildServiceProvider();

        IServiceProvider providerB = shared
            .SetupSubInstance()
            .OverrideConfig("Run:Tenant", "B")
            .BuildServiceProvider();

        Assert.Equal("A", providerA.GetRequiredService<IConfiguration>()["Run:Tenant"]);
        Assert.Equal("B", providerB.GetRequiredService<IConfiguration>()["Run:Tenant"]);
    }

    [Fact]
    public async Task IntegrationWithTimelineRuns_UsesBuiltServiceProvider()
    {
        Timeline timeline = Timeline.Create().Build();
        IServiceProvider provider = ConfigInstance
            .Create()
            .BuildServiceProvider();

        TimelineRun run = await timeline.SetupRun(provider).RunAsync();

        run.EnsureRanToCompletion();
    }

    private sealed record ReadmeDependency(string Mode);
}