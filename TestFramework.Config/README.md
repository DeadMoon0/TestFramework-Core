# TestFramework.Config

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

// The provider owns every singleton it creates, so you own the provider: dispose it.
using ServiceProvider serviceProvider = ConfigInstance
	.FromJsonFile("appsettings.test.json")
	.OverrideConfig("FeatureFlags:UseMockService", "true")
	.AddService((services, configuration) =>
	{
		services.AddHttpClient();
		services.AddSingleton<IMyDependency, MyDependency>();
	})
	.BuildServiceProvider();
```

## Which Configuration Abstraction Do I Use?

For ordinary timeline tests, treat `ConfigInstance` as the default and primary entry point.

- Use `ConfigInstance` when you want to prepare configuration, register services, build an `IServiceProvider`, and pass that provider into `SetupRun(...)`.
- Treat package-specific typed stores such as Azure's `ConfigStore<T>` as advanced runtime services that may live inside that provider. They are not a second root setup model for most consumers.

The practical ownership rule is:

- `ConfigInstance` owns the run setup container.
- typed stores live inside that container when a module needs named resource records at runtime.

| If your question is... | Prefer |
|---|---|
| "How do I prepare config and services for `SetupRun(...)`?" | `ConfigInstance` |
| "How does Azure/SQL/Container look up a named runtime record like `MainDb`?" | module-owned `ConfigStore<T>` inside DI |
| "Do I need to choose one model for the whole test?" | no, start with `ConfigInstance`; typed stores are advanced runtime services, not a second root setup style |

If you need both, you still start with `ConfigInstance`, then let your module register or resolve the typed store through DI.

## Decision Guide

- You have JSON files, a few overrides, or normal service registration: use `ConfigInstance`.
- You want a reusable shared base with per-test variants: build a `ConfigInstance`, then call `SetupSubInstance()`.
- A richer module such as Azure or SQL needs named resource records like `MainSql` or `Default`: keep using `ConfigInstance` for setup, and treat the typed store as an implementation detail consumed from DI.

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

What a sub-instance inherits, and what it can change:

```
Init new ConfigInstance (BaseConfig, BaseServices)
                         |           \- BaseServices: from another ConfigInstance, or empty
                         \- BaseConfig:   from JSON, from another ConfigInstance, or empty

Configure the ConfigInstance \
                             |- Override configs
                             \- Add services

Create SubConfigInstance (BaseConfig, BaseServices)
                          |           \- BaseServices: inherited from the parent instance
                          \- BaseConfig:   inherited from the parent instance
```

Each `BuildServiceProvider()` on a sub-instance produces an independent provider, which is what keeps
one test's service state out of another's.

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
- `BuildServiceProvider()`: build a `ServiceProvider` for `SetupRun(...)`. **The caller owns the returned provider and must dispose it** — it holds every singleton it created. The return type is the concrete `ServiceProvider`, not `IServiceProvider`, so the compiler can point that out.

## Error Contract

- `ConfigInstance.FromJsonFile(path)` throws `FileNotFoundException` when the file does not exist.
- `ConfigInstance.FromJsonFile(path)` throws `InvalidDataException` when the JSON content cannot be parsed.
- Service-registration delegates added through `AddService(...)` run during `BuildServiceProvider()` and any exception they throw is propagated to the caller.

## Advanced Usage Notes

- Prefer `Build()` when you want a reusable base configuration that can spawn multiple sub-instances.
- Prefer `SetupSubInstance()` when tests share most configuration but need a few targeted overrides.
- Use the `AddService((services, configuration) => ...)` overload when service registration depends on effective configuration values after overrides have been applied.
- If a module also uses a typed registry inside DI, keep the ownership boundary clear: `ConfigInstance` prepares the provider, the typed registry serves that module's runtime lookup needs.

## Target Framework

- .NET 8 (`net8.0`)