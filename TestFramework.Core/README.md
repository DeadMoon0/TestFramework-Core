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
using TestFramework.Core.Variables;
using Xunit;

public class CoreSample
{
    [Fact]
    public async Task RunTimeline()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Alex"))
            .Transform("greeting", Var.Ref<string>("name"), n => $"Hello {n}")
            .AssertVariable(Var.Ref<string>("greeting"), g => g == "Hello Alex")
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .RunAsync();

        run.EnsureRanToCompletion();
        run.Variable<string>("greeting").Should().Exist().And().Be("Hello Alex");
    }
}
```

## Common Building Blocks

- `Timeline.Create()` to start the builder
- `SetVariable`, `Transform`, `AssertVariable` for variable-driven data flow
- `Trigger(...)` and `WaitForEvent(...)` for actions and external synchronization
- `WithTimeOut(...)`, `WithRetry(...)` for reliability on unstable systems

## Typical Pattern

1. Build timeline once (usually static in test classes).
2. Create a run with `SetupRun(...)`.
3. Add runtime variables/artifacts if needed.
4. Run with `RunAsync()`.
5. Assert with `EnsureRanToCompletion()` and variable/artifact checks.

## Target Framework

- .NET 8 (`net8.0`)
