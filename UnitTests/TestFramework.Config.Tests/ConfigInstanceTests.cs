using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using TestFramework.Config;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

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

    [Fact]
    public void FromJsonFile_ThrowsFileNotFoundException_WhenFileIsMissing()
    {
        string missingFile = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
            ConfigInstance.FromJsonFile(missingFile));

        Assert.Contains(Path.GetFileName(missingFile), exception.Message);
    }

    [Fact]
    public void FromJsonFile_ThrowsInvalidDataException_WhenJsonIsInvalid()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"invalid-config-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, "{ \"App\": { \"Mode\": } }");

            Assert.Throws<InvalidDataException>(() => ConfigInstance.FromJsonFile(tempFile));
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
    public void BuildServiceProvider_PropagatesRegistrationFailures()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ConfigInstance.Create()
                .AddService((services, configuration) => throw new InvalidOperationException("boom"))
                .BuildServiceProvider());

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task ConfigPersistentEnvironmentContext_LayersRunConfig_AndSupportsTimelineSetupRunExtension()
    {
        await using ConfigPersistentEnvironmentContext<ConfigPersistentTestSetup> persistent =
            await ConfigPersistentEnvironmentContext<ConfigPersistentTestSetup>.CreateAsync();

        ConfigInstance runConfig = persistent.CreateRunConfig(builder => builder.OverrideConfig("App:Mode", "run"));

        IConfiguration configuration = runConfig.BuildServiceProvider().GetRequiredService<IConfiguration>();
        Assert.Equal("run", configuration["App:Mode"]);

        Timeline timeline = Timeline.Create()
            .Trigger(new ConfigNoOpStep())
            .Build();

        TimelineRun run = await timeline.SetupRun(runConfig)
            .SetEnv(persistent.CreateEnvironment())
            .RunAsync();

        run.EnsureRanToCompletion();
    }

    private sealed record BoundOptions(string Mode);

    private sealed record MarkerService(string Value);

    private sealed class ConfigPersistentTestSetup : IConfigPersistentEnvironmentSetup
    {
        public IEnvironmentProvider CreateEnvironment() => new ConfigPersistentEnvironment();

        public ConfigInstance CreatePersistentConfig() => ConfigInstance.Create()
            .OverrideConfig("App:Mode", "persistent")
            .Build();

        public IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers() => ["network"];
    }

    private sealed class ConfigPersistentEnvironment : EnvironmentProviderBase
    {
        public ConfigPersistentEnvironment()
        {
            AddComponent(new ConfigLoggingEnvComponent("network") { ReuseModeOverride = EnvComponentReuseMode.PersistentContext });
        }
    }

    private sealed class ConfigLoggingEnvComponent(string identifier) : EnvComponent
    {
        public EnvComponentReuseMode ReuseModeOverride { get; init; } = EnvComponentReuseMode.PerRun;

        public override EnvComponentIdentifier Id => identifier;

        public override EnvComponentReuseMode ReuseMode => ReuseModeOverride;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult((object?)$"state:{Id}");

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ConfigNoOpStep : Step<EmptyStepResultContext>
    {
        public override string Name => "ConfigNoOp";
        public override string Description => "ConfigNoOp";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new ConfigNoOpStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}