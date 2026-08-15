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
        const string expectedMessage = "Action executed";

        Timeline timeline = Timeline.Create()
            .Trigger(SimpleExt.Trigger.Action(() => message = expectedMessage))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(expectedMessage, message);
    }
}
```

## Variable-Aware Action

```csharp
using TestFramework.Core.Variables;
using TestFramework.Simple;

Timeline timeline = Timeline.Create()
    .SetVariable("name", Var.Const("Alex"))
    .Trigger(SimpleExt.Trigger.Action(vars => Console.WriteLine($"Hello {vars[new VariableIdentifier("name")]}"), Var.Ref<string>("name")))
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
    .Trigger(SimpleExt.Trigger.Action((vars, artifacts) =>
    {
        string? name = (string?)vars[new VariableIdentifier("name")];
        ArtifactInstanceGeneric payload = artifacts[payloadArtifact];
        Console.WriteLine($"Processing {name} with artifact {payload.Identifier}");
    }, [Var.Ref<string>("name")], payloadArtifact))
    .Build();
```

## Full-Context Action

```csharp
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.Simple;

Timeline timeline = Timeline.Create()
    .Trigger(SimpleExt.Trigger.Action((serviceProvider, logger, vars, artifacts) =>
    {
        logger.LogInformation("Executing inline action with {VariableCount} variables and {ArtifactCount} artifacts.", vars.Count, artifacts.Count);
    }, [Var.Ref<string>("name")]))
    .Build();
```

## Announcing Something Mid-Run

`SimpleExt.Trigger.Message(msg, caption)` writes `[caption] msg` to the run log. It works everywhere
and blocks nothing, so it is the right choice for anything that just needs to be visible in the
output.

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(SimpleExt.Trigger.Message("Waiting for the downstream system", "Progress"))
    .Build();
```

## Windows MessageBox Behavior

`SimpleExt.Trigger.MessageBox(...)` is Windows-only because it calls `user32.dll`. It is for a human
sitting in front of the run.

- Use it only on Windows test machines.
- Do not rely on it for unattended CI runs. Set `TESTFRAMEWORK_MESSAGEBOX=off` in CI and the default
  invoker returns immediately without showing a dialog, so an agent with nobody to click OK does not
  sit on a modal window until the step times out.
- Replace `MessageBoxTrigger.Invoker` to route the text somewhere else, or to assert on it in a test.
- Prefer `Message(...)` — the same shape, logged rather than shown — when you need a cross-platform
  step, or `Action(...)` for arbitrary inline work.

## Handling Failures

- `Action(...)` fails immediately if you pass a null delegate.
- Variable-based overloads require identifiers for every supplied variable reference.
- `MessageBox(...)` requires Windows and will not behave correctly on non-Windows platforms.

## Includes

- `SimpleExt.Trigger.Action(...)` for inline custom actions
- `SimpleExt.Trigger.Message(...)` for a captioned line in the run log, on any platform
- `SimpleExt.Trigger.MessageBox(...)` for simple Windows message box flows

## Target Framework

- .NET 8 (`net8.0`)
