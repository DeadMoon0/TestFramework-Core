# TestFrameworkSimple

## Introduction

TestFrameworkSimple is an extension package for TestFrameworkCore.

If you are new: TestFrameworkCore provides the timeline engine.
This package adds easy, lightweight triggers so you can inject custom actions quickly without building full step classes.

Simple triggers for quick custom actions inside a timeline.

Use TestFrameworkSimple when you want to run lightweight inline logic without creating full custom step classes.

## Install

```bash
dotnet add package TestFrameworkSimple
```

## Quick Start

```csharp
using TestFrameworkCore.Timelines;
using TestFrameworkSimple;
using Xunit;

public class SimpleSample
{
    [Fact]
    public async Task InlineAction()
    {
        string? message = null;

        Timeline timeline = Timeline.Create()
            .Trigger(Simple.Trigger.Action(() =>
            {
                message = "Action executed";
            }))
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal("Action executed", message);
    }
}
```

## Variable-Aware Action

```csharp
using TestFrameworkCore.Variables;

Timeline timeline = Timeline.Create()
    .SetVariable("name", Var.Const("Alex"))
    .Trigger(Simple.Trigger.Action(vars =>
    {
        var name = (string?)vars[new VariableIdentifier("name")];
        Console.WriteLine($"Hello {name}");
    }, Var.Ref<string>("name")))
    .Build();
```

## Includes

- `Simple.Trigger.Action(...)` for inline custom actions
- `Simple.Trigger.MessageBox(...)` for simple Windows message box flows

## Target Framework

- .NET 8 (`net8.0`)
