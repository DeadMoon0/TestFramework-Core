# TestFramework.Simple

`TestFramework.Simple` is an extension package for `TestFramework.Core`.

It adds lightweight triggers for common cases where you do not want to create a full custom `Step<T>` class.

## Install

```bash
dotnet add package TestFramework.Simple
```

## Quick Start

```csharp
using TestFramework.Core.Timelines;
using TestFramework.Simple;
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

        TimelineRun run = await timeline
            .SetupRun()
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal("Action executed", message);
    }
}
```

## Variable-Aware Action

```csharp
using TestFramework.Core.Variables;
using TestFramework.Simple;

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
