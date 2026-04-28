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

The package exposes additional public types for artifacts, environment integration, debugging, and the fluent builder composition model, but those are advanced surfaces. If you are writing tests rather than framework extensions, prefer the timeline builder, `Var`, `TimelineRun`, and the assertion handles as your main API.

## Extension-Facing Surface

You only need the lower-level public abstractions when you are extending the framework itself, for example by adding:

- custom triggers or events
- artifact describers and references
- environment-provider integrations
- runtime or debugging integrations

Those advanced surfaces are supported by the architecture docs, but they are secondary to the consumer workflow above.

## Typical Pattern

1. Build timeline once (usually static in test classes).
2. Create a run with `SetupRun(...)`.
3. Add runtime variables/artifacts if needed.
4. Run with `RunAsync()`.
5. Assert with `EnsureRanToCompletion()` and variable/artifact checks.

## Target Framework

- .NET 8 (`net8.0`)
