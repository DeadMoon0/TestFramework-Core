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

Override precedence is last-write-wins within the active builder. A sub-instance starts with the parent instance's merged values and registrations, then applies its own overrides and additions on top.

## Integration With Timeline Runs

```csharp
using TestFramework.Config;
using TestFramework.Core.Timelines;

private static readonly Timeline _timeline = Timeline.Create().Build();

var provider = ConfigInstance
	.Create()
	.BuildServiceProvider();

TimelineRun run = await _timeline.SetupRun(provider).RunAsync();

run.EnsureRanToCompletion();
```

## API Summary

- `ConfigInstance.FromJsonFile(path)`: start with JSON-backed configuration
- `ConfigInstance.Create()`: start from an empty configuration/service state
- `OverrideConfig(...)`: replace/add config values
- `AddService(...)`: register dependencies
- `Build()`: materialize a reusable `ConfigInstance`
- `BuildServiceProvider()`: build `IServiceProvider` for `SetupRun(...)`

## Error Contract

- `ConfigInstance.FromJsonFile(path)` throws `FileNotFoundException` when the file does not exist.
- `ConfigInstance.FromJsonFile(path)` throws `InvalidDataException` when the JSON content cannot be parsed.
- Service-registration delegates added through `AddService(...)` run during `BuildServiceProvider()` and any exception they throw is propagated to the caller.

## Advanced Usage Notes

- Prefer `Build()` when you want a reusable base configuration that can spawn multiple sub-instances.
- Prefer `SetupSubInstance()` when tests share most configuration but need a few targeted overrides.
- Use the `AddService((services, configuration) => ...)` overload when service registration depends on effective configuration values after overrides have been applied.

## Target Framework

- .NET 8 (`net8.0`)