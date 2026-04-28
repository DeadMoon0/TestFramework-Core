# TestFramework Core

TestFramework is a timeline-based framework for integration-style tests.
You define test workflows once (build phase), execute them with run-specific inputs (run phase), and validate immutable results (result phase).

This repository is the foundation of the wider TestFramework ecosystem.

## Packages

- `TestFramework.Core`: timeline engine, steps, variables, artifacts, assertions
- `TestFramework.Config`: `ConfigInstance` for configuration and dependency injection setup
- `TestFramework.Simple`: lightweight triggers for fast onboarding and simple flows

## Install Via NuGet

Install the minimal set first:

```bash
dotnet add package TestFramework.Core
```

Add optional companion packages as needed:

```bash
dotnet add package TestFramework.Config
dotnet add package TestFramework.Simple
```

## Quickstart

The following example shows the end-to-end path that a new team member needs:
installation, configuration via `ConfigInstance`, timeline definition, run execution, and assertion.

```csharp
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;

public class SampleIntegrationTest
{
	private const string InputValue = "Alex";

	private static readonly Timeline _timeline = Timeline.Create()
		.SetVariable("name", Var.Const(InputValue))
		.Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
		.AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == $"Hello {InputValue}")
		.Build();

	[Fact]
	public async Task CanRunTimeline()
	{
		var serviceProvider = ConfigInstance
			.Create()
			.OverrideConfig("TestSettings:Environment", "Local")
			.AddService(services =>
			{
				services.AddSingleton<ISystemClock, SystemClock>();
			})
			.BuildServiceProvider();

		TimelineRun run = await _timeline.SetupRun(serviceProvider).RunAsync();

		run.EnsureRanToCompletion();

		using (var assertionScope = run.AssertionScope())
		{
			run.Variable<string>("greeting").Should().Exist().And().Be($"Hello {InputValue}");
		}
	}

	private interface ISystemClock { }
	private sealed class SystemClock : ISystemClock { }
}
```

## Core Concepts

### Timeline

A timeline is the test workflow definition.
You compose actions in order and call `Build()` to freeze the plan.

### Steps

Steps are executable units (trigger external systems, wait for events, transform or assert data).
Step-level options such as retry and timeout can be applied fluently.

### Variables

Variables are the data flow channel between steps and between builder/run phases.
Use `Var.Const(...)` for constants and `Var.Ref<T>(...)` for runtime-resolved values.

### Artifacts

Artifacts represent external resources that are created, registered, versioned, and cleaned up as part of the run.
This enables deterministic setup/cleanup in integration tests.

## Public Contract Layers

Treat the Core surface in this order:

### Consumer-First API

This is the primary API most test authors should learn first.

- `Timeline.Create()`
- fluent builder verbs such as `SetVariable`, `Transform`, `Trigger`, `WaitForEvent`, `AssertVariable`, `Conditional`, and `ForEach`
- `Build()`, `SetupRun(...)`, and `RunAsync()`
- `TimelineRun`, `StepHandle`, assertion handles, and `Var`

If you are writing tests rather than extending the framework, this is the contract to optimize for.

### Advanced Extension API

These surfaces are intended for package authors and deeper framework integrations:

- artifact describers, artifact references, and artifact data types
- environment-provider abstractions and environment requirements
- event base types and selected step/runtime options

These APIs are valid but are not the recommended starting point for new consumers.

### Visible Scaffolding

Some public interfaces are currently exposed because the fluent builder is composed from many action interfaces.
They are part of the visible package surface today, but they are not the recommended mental model for learning Core.
When in doubt, follow the `Timeline.Create()` path and the runnable showroom examples first.

## Documentation Map

- Architecture overview (arc42): [Documentation/Arc42.md](./Documentation/Arc42.md)
- Core architecture details: [Documentation/CoreArchitecture.md](./Documentation/CoreArchitecture.md)
- Concept deep dive: [Documentation/Documentation.md](./Documentation/Documentation.md)
- Core package guide: [TestFramework.Core/README.md](./TestFramework.Core/README.md)
- Config package guide: [TestFramework.Config/README.md](./TestFramework.Config/README.md)
- Simple package guide: [TestFramework.Simple/README.md](./TestFramework.Simple/README.md)

Recommended reading order for new users:

1. This README for the consumer-first workflow.
2. `TestFramework.Core/README.md` for package-local usage.
3. Showroom examples for runnable patterns.
4. `Documentation/Arc42.md` only when you need extension or architecture detail.

## Examples

For runnable consumer examples see the showroom repository and start with:

- `TestFramework.Showroom.Basic/01_MinimalTimeline.cs`
- `TestFramework.Showroom.Basic/04_Variables.cs`
- `TestFramework.Showroom.Basic/09_StepValidations.cs`

## Related Repositories

- [TestFramework-Azure](https://github.com/DeadMoon0/TestFramework-Azure) for Azure-specific triggers, events, and artifact helpers
- [TestFramework-LocalIO](https://github.com/DeadMoon0/TestFramework-LocalIO) for local machine commands, files, and IO-driven workflows
- [TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) for runnable examples across the ecosystem

## CI Pull Requests

- Pull requests run unit tests through the GitHub Actions workflow `unit-tests`.
- If branch protection requires status checks, `unit-tests` must pass before merge.

Local pre-PR test commands:

```bash
dotnet test UnitTests/TestFramework.Config.Tests/TestFramework.Config.Tests.csproj --configuration Release
dotnet test UnitTests/TestFramework.Core.Tests/TestFramework.Core.Tests.csproj --configuration Release
dotnet test UnitTests/TestFramework.Simple.Tests/TestFramework.Simple.Tests.csproj --configuration Release
```
