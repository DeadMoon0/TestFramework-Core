# TestFramework Core

## What TestFramework Is

TestFramework is a timeline-based test framework for building integration-style test workflows.
It gives you a fluent way to compose triggers, waits, variables, artifacts, and assertions into a reproducible execution flow.

This solution is the foundation of the wider TestFramework ecosystem.

## What This Solution Covers

TestFramework-Core contains the packages that define the main runtime and the first layers around it:

- `TestFramework.Core` for the timeline engine and execution model
- `TestFramework.Config` for configuration loading and service setup
- `TestFramework.Simple` for lightweight helpers and simpler entry points

If you are new to the ecosystem, this is the best place to understand the core mental model before adding environment-specific extensions.

## What You Can Do With It

With this solution you can:

- build timelines that execute test workflows step by step
- manage variables, artifacts, and execution state across a run
- add assertions and validation logic to integration-style tests
- prepare a base runtime that other solutions such as Azure and LocalIO extend

## Related Repositories

- [TestFramework-Azure](https://github.com/DeadMoon0/TestFramework-Azure) for Azure-specific triggers, events, and artifact helpers
- [TestFramework-LocalIO](https://github.com/DeadMoon0/TestFramework-LocalIO) for local machine commands, files, and IO-driven workflows
- [TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) for runnable examples across the ecosystem

## Where To Start

- Start with the package overview in [TestFramework.Core/README.md](./TestFramework.Core/README.md)
- Use [TestFramework.Simple/README.md](./TestFramework.Simple/README.md) if you want a lighter first contact with the framework
- Then open the Showroom repository and begin with `TestFramework.Showroom.Basic/01_MinimalTimeline.cs`, `04_Variables.cs`, and `09_StepValidations.cs`
