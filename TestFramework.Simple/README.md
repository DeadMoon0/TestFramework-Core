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

## Choosing An `Action(...)` Overload

Use the smallest overload that matches the information you need:

- `Action(Action action)` when the step only needs to run code.
- `Action(Action<Dictionary<VariableIdentifier, object?>> action, params VariableReferenceGeneric[] variables)` when the step only needs resolved variables.
- `Action(Action<Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)` when the step needs both variables and artifacts.
- `Action(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)` when the step also needs dependency-injected services or logging.

The richer overloads intentionally trade some simplicity for flexibility. Variable and artifact values are exposed through dictionaries keyed by their identifiers.

## Artifact-Aware Action

```csharp
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;
using TestFramework.Simple;

ArtifactIdentifier payloadArtifact = new("payload");

Timeline timeline = Timeline.Create()
    .Trigger(Simple.Trigger.Action(
        (vars, artifacts) =>
        {
            string? name = (string?)vars[new VariableIdentifier("name")];
            ArtifactInstanceGeneric payload = artifacts[payloadArtifact];
            Console.WriteLine($"Processing {name} with artifact {payload.Identifier}");
        },
        [Var.Ref<string>("name")],
        payloadArtifact))
    .Build();
```

## Full-Context Action

```csharp
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.Simple;

Timeline timeline = Timeline.Create()
    .Trigger(Simple.Trigger.Action(
        (serviceProvider, logger, vars, artifacts) =>
        {
            logger.LogInformation("Executing inline action with {VariableCount} variables and {ArtifactCount} artifacts.", vars.Count, artifacts.Count);
        },
        [Var.Ref<string>("name")]))
    .Build();
```

## Windows MessageBox Behavior

`Simple.Trigger.MessageBox(...)` is Windows-only because it calls `user32.dll`.

- Use it only on Windows test machines.
- Do not rely on it for unattended CI runs.
- Prefer `Action(...)` when you need a cross-platform inline step.

## Handling Failures

- `Action(...)` fails immediately if you pass a null delegate.
- Variable-based overloads require identifiers for every supplied variable reference.
- `MessageBox(...)` requires Windows and will not behave correctly on non-Windows platforms.

## Includes

- `Simple.Trigger.Action(...)` for inline custom actions
- `Simple.Trigger.MessageBox(...)` for simple Windows message box flows

## Target Framework

- .NET 8 (`net8.0`)
