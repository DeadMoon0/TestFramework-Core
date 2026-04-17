using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;

namespace TestFramework.Config.Tests;

public class ConfigInstanceTests
{
    [Fact]
    public void BuildServiceProvider_UsesLatestOverrideValuesForConfigurationAndServices()
    {
        ConfigInstance config = ConfigInstance.Create()
            .OverrideConfig("App:Mode", "base")
            .AddService((services, configuration) =>
            {
                services.AddSingleton(new BoundOptions(configuration["App:Mode"] ?? string.Empty));
            })
            .Build();

        IServiceProvider provider = config
            .SetupSubInstance()
            .OverrideConfig("App:Mode", "child")
            .BuildServiceProvider();

        IConfiguration resolvedConfiguration = provider.GetRequiredService<IConfiguration>();
        BoundOptions options = provider.GetRequiredService<BoundOptions>();

        Assert.Equal("child", resolvedConfiguration["App:Mode"]);
        Assert.Equal("child", options.Mode);
    }

    [Fact]
    public void BuildServiceProvider_AppliesParameterlessServiceRegistrations()
    {
        IServiceProvider provider = ConfigInstance.Create()
            .AddService(services => services.AddSingleton(new MarkerService("created")))
            .BuildServiceProvider();

        MarkerService marker = provider.GetRequiredService<MarkerService>();

        Assert.Equal("created", marker.Value);
    }

    [Fact]
    public void FromJsonFile_LoadsJsonValuesIntoConfiguration()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, """
                {
                  "App": {
                    "Mode": "json",
                    "Retries": 3
                  },
                  "Ignored": null
                }
                """);

            IServiceProvider provider = ConfigInstance.FromJsonFile(tempFile).BuildServiceProvider();
            IConfiguration configuration = provider.GetRequiredService<IConfiguration>();

            Assert.Equal("json", configuration["App:Mode"]);
            Assert.Equal("3", configuration["App:Retries"]);
            Assert.Null(configuration["Ignored"]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed record BoundOptions(string Mode);

    private sealed record MarkerService(string Value);
}