# TestFramework.Core

`TestFramework.Core` is the timeline engine of the TestFramework ecosystem.

It provides the public API to:

- define integration-test workflows
- execute them with runtime inputs
- assert outcomes from an immutable run result

## Install

```bash
dotnet add package TestFramework.Core
```

## Quick Start

```csharp
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;

public class CoreSample
{
	private const string InputValue = "Alex";

	private static readonly Timeline _timeline = Timeline.Create()
		.SetVariable("name", Var.Const(InputValue))
		.Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
		.AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == $"Hello {InputValue}")
		.Build();

    [Fact]
    public async Task RunTimeline()
    {
        TimelineRun run = await _timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        using (var assertionScope = run.AssertionScope())
        {
            run.Variable<string>("greeting").Should().Exist().And().Be($"Hello {InputValue}");
        }
    }
}
```

## Common Building Blocks

- `Timeline.Create()` to start the builder
- `SetVariable`, `Transform`, `AssertVariable` for variable-driven data flow
- `Trigger(...)` and `WaitForEvent(...)` for actions and external synchronization
- `WithTimeOut(...)`, `WithRetry(...)` for reliability on unstable systems

## Consumer-First Contract

For most users, the Core contract is intentionally small:

1. Start with `Timeline.Create()`.
2. Compose fluent steps and modifiers.
3. Freeze the plan with `Build()`.
4. Create a run with `SetupRun(...)`.
5. Execute with `RunAsync()` and assert through `TimelineRun`.

The scope split matters:

- `Build()` usually belongs at class scope because it produces the reusable timeline definition.
- `SetupRun(...)` usually belongs at method scope because each call creates a per-run builder with run-specific services, variables, artifacts, or output wiring.

The package exposes additional public types for artifacts, environment integration, debugging, and the fluent builder composition model, but those are advanced surfaces. If you are writing tests rather than framework extensions, prefer the timeline builder, `Var`, `TimelineRun`, and the assertion handles as your main API.

## Fluent API Discovery

The fluent API is one logical surface even though it is composed internally from several public interfaces.

For normal usage, treat these as the only concepts that matter:

- `Timeline.Create()` starts composition
- fluent builder verbs add steps and modifiers
- `Build()` freezes the reusable definition
- `SetupRun(...)` creates a per-run builder
- `RunAsync()` executes the run

The lower-level action interfaces remain public for compatibility and extension reasons, but they are intentionally de-emphasized in IntelliSense. If you discover those types directly, prefer returning to `ITimelineBuilder`, `ITimelineBuilderModifier`, and the fluent usage examples rather than learning the API through the interface lattice.

## Extension-Facing Surface

You only need the lower-level public abstractions when you are extending the framework itself, for example by adding:

- custom triggers or events
- artifact describers and references
- environment-provider integrations
- runtime or debugging integrations

Those advanced surfaces are supported by the architecture docs, but they are secondary to the consumer workflow above.

## Timeline Debugging

The recommended debugging path depends on what you need to see:

1. Name important steps so failures and assertions point to stable labels.
2. Pass `ITestOutputHelper` into `SetupRun(...)` when you want the timeline log in the test output stream.
3. Inspect the completed `TimelineRun` for stage state, step results, variables, and artifacts.

For most users, that post-run inspection path is the supported debugging workflow.
The lower-level debugger integration seam (`IRunDebugger` and related state types) remains available for custom tooling, but it is an advanced integration surface rather than the primary learning path.

## Typical Pattern

1. Build timeline once (usually static in test classes or other reusable class scope).
2. Create a run with `SetupRun(...)` inside the test or method that is about to execute it.
3. Add runtime variables/artifacts if needed.
4. Run with `RunAsync()`.
5. Assert with `EnsureRanToCompletion()` and variable/artifact checks.

## Persistent Environments

Most timelines should keep environment creation per run.
When a suite repeatedly needs the same expensive environment slice, Core also exposes `PersistentEnvironmentContext<TSetup>` as the lower-level reuse primitive.

Use it when all of the following are true:

- the environment shape is stable across many runs
- some components are expensive enough that recreating them dominates runtime
- those components can safely opt into `EnvComponentReuseMode.PersistentContext`

The model is:

1. `TSetup.CreateEnvironment()` describes the full environment instance that future runs should receive.
2. `TSetup.GetPersistentComponentIdentifiers()` selects the component roots that should be realized once and reused.
3. `PersistentEnvironmentContext<TSetup>.CreateEnvironment()` produces fresh run environments with the persistent runtime state seeded back in.

Higher-level packages may wrap this primitive with package-specific helpers. In the container stack, `DockerAzureHostedCollectionFixture<TState>` is the xUnit-facing example of that pattern.

## Target Framework

- .NET 8 (`net8.0`)
