# TestFrameworkCore

## Introduction

TestFrameworkCore is the foundation of the whole TestFramework package family.

If you are new: this package gives you the timeline engine that executes your test workflow step by step.
Other packages like TestFrameworkAzure, TestFrameworkLocalIO, and TestFrameworkSimple plug into this core engine.

In short:
- build a timeline
- run it
- assert variables and artifacts

Core timeline engine for integration-style test workflows.

TestFrameworkCore gives you a fluent pipeline to:
- trigger actions and steps
- wait for events
- manage variables and artifacts
- run and assert execution results

## Install

```bash
dotnet add package TestFrameworkCore
```

## Quick Start

```csharp
using TestFrameworkCore.Timelines;
using TestFrameworkCore.Variables;
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
    }
}
```

## Common Building Blocks

- `Timeline.Create()` to start a timeline
- `SetVariable`, `Transform`, `AssertVariable` for data flow
- `Trigger(...)` for executing steps
- `WaitForEvent(...)` for polling/event-based waits
- `WithTimeOut(...)`, `WithRetry(...)` for resilience

## Typical Pattern

1. Build timeline once (usually static in test classes).
2. Create a run with `SetupRun(...)`.
3. Add runtime variables/artifacts if needed.
4. Run with `RunAsync()`.
5. Assert with `EnsureRanToCompletion()` and variable/artifact checks.

## Target Framework

- .NET 8 (`net8.0`)
