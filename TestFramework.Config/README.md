# TestFramework Config

`TestFramework.Config` provides a simple, composable way to prepare
`IServiceProvider` and `IConfiguration` for timeline runs.

Use it when your integration tests need environment-specific configuration,
service registration, or per-test overrides.

## Install

```bash
dotnet add package TestFramework.Config
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;

var serviceProvider = ConfigInstance
	.FromJsonFile("appsettings.test.json")
	.OverrideConfig("FeatureFlags:UseMockService", "true")
	.AddService((services, configuration) =>
	{
		services.AddHttpClient();
		services.AddSingleton<IMyDependency, MyDependency>();
	})
	.BuildServiceProvider();
```

## Typical Pattern

Use a shared base config and derive per-test variants:

```csharp
using TestFramework.Config;

ConfigInstance shared = ConfigInstance
	.FromJsonFile("appsettings.test.json")
	.Build();

var providerA = shared
	.SetupSubInstance()
	.OverrideConfig("Run:Tenant", "A")
	.BuildServiceProvider();

var providerB = shared
	.SetupSubInstance()
	.OverrideConfig("Run:Tenant", "B")
	.BuildServiceProvider();
```

## Integration With Timeline Runs

```csharp
using TestFramework.Config;
using TestFramework.Core.Timelines;

Timeline timeline = Timeline.Create().Build();

var provider = ConfigInstance
	.Create()
	.BuildServiceProvider();

TimelineRun run = await timeline
	.SetupRun(provider)
	.RunAsync();

run.EnsureRanToCompletion();
```

## API Summary

- `ConfigInstance.FromJsonFile(path)`: start with JSON-backed configuration
- `ConfigInstance.Create()`: start from an empty configuration/service state
- `OverrideConfig(...)`: replace/add config values
- `AddService(...)`: register dependencies
- `Build()`: materialize a reusable `ConfigInstance`
- `BuildServiceProvider()`: build `IServiceProvider` for `SetupRun(...)`

## Target Framework

- .NET 8 (`net8.0`)