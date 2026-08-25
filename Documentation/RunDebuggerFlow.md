# RunDebugger Flow

This document describes the current RunDebugger flow in Core, including when each signal is emitted, how retry semantics are encoded, how debugger consumers derive execution logs from the state machine, and how multiple debugger consumers are coordinated.

## Main pieces

- `TimelineRunBuilder` builds the run structure, creates the debugger session, emits run and stage lifecycle transitions, and always closes the session.
- `DebuggingRunSession` is the Core-side state-machine adapter. It owns the `SessionId`, the current step execution context, and translates runtime activity into `IRunDebugger` calls.
- `CoreRunner` executes stage layers and step attempts. It is the main source of step transitions and breakpoint waits.
- `VariableStore` and `ArtifactStore` emit value updates whenever runtime values change.
- `IRunDebugger` is the transport contract. The current protocol uses `InitTimelineRun`, `EntityTransition`, `ValueUpdate`, `LogEntry`, `Assertion`, `BreakpointHitRequest`, and `TimelineRunFinished`.
- `ScopedLogger` is now only a thin shell over debugger signaling. Freeform log entries are only valid while a step iteration is active.
- `CommonDebugger` builds a fan-out debugger endpoint. That lets one run publish to multiple consumers such as the pipe adapter and the output-helper adapter at the same time.

## Signal order

### 1. Run structure initialization

`TimelineRunBuilder.RunAsync()` preprocesses stages, validates IO, builds the runtime `TimelineRunStructure`, and then calls:

- `DebuggingRunSession.InitSessionAsync(...)`
- `DebuggingRunSession.TransitionRunAsync(Initialized)`

`InitSessionAsync(...)` emits `SignalInitTimelineRunAsync(sessionId, name, projectPath, runStructure)`.

The run structure snapshot contains:

- all current variables and artifacts as `DebugValue`
- all stages and steps, including their IO contracts and execution options

This is the point where DebugUI can build the full run tree before execution starts.

### 2. Run start

Right before stage execution begins, `TimelineRunBuilder` emits:

- `Run: Initialized -> Running`

This is the top-level transition that marks the runtime as active.

### 3. Stage lifecycle

For each stage in order, `TimelineRunBuilder` emits:

- `Stage: Initialized -> Running` before `CoreRunner.RunStage(...)`
- `Stage: Running -> Complete` or `Stage: Running -> Error` after the stage finishes

Stages are still entered serially, even though steps inside a stage may run in parallel.

### 4. Step lifecycle

`CoreRunner.ExecuteStepAsync(...)` is the source of step-level transitions.

Before the first attempt:

- a step execution context is opened with `BeginStepExecutionContext(stageName, stepIndex)`
- `SignalAndWaitBreakpointHitAsync(...)` is called

That breakpoint request happens before the first `Running` transition. The host can inspect state and then reply with `BreakpointHitContinue`.

For each attempt:

- first attempt emits `Step: Initialized -> Running`
- retry attempt emits `Step: WaitingForRetry -> Running`

After the attempt finishes, Core maps the `StepState` to a debugger lifecycle state:

- `Complete -> Complete`
- `Error -> Error`
- `Timeout -> Timeout`
- `Skipped -> Skipped`

Then Core emits one of these two shapes:

- final attempt: `Step: Running -> Complete|Error|Timeout|Skipped`
- retryable failure: `Step: Running -> WaitingForRetry` with `OutcomeState = Error|Timeout|...`

The important rule is:

- `State` is the current step lifecycle state
- `OutcomeState` is the result of the just-finished attempt

That is why a retrying step can be in `WaitingForRetry` while still preserving the fact that the last attempt ended in `Error` or `Timeout`.

### 5. Value updates

Value updates are emitted by the runtime stores, not by `CoreRunner` directly.

`VariableStore` emits:

- `SignalValueUpdateAsync(sessionId, variableName, Variable, stage?, stepId?, envelope)`

`ArtifactStore` emits:

- `SignalValueUpdateAsync(sessionId, artifactName, Artifact, stage?, stepId?, envelope)`

`DebuggingRunSession` uses an `AsyncLocal` execution context so value updates produced during a step carry:

- `Stage`
- `StepId`

If a value update happens outside an active step context, the transport still works, but the update is run-scoped instead of step-scoped.

### 6. Freeform step log entries

`ScopedLogger` is no longer the mechanism that writes framework execution logs. Instead, it only forwards freeform log text emitted during an active step iteration.

Each log entry carries:

- `OccurredAtUtc`
- `Level`
- `EventName`
- rendered `Message`
- rendered `Lines`
- `IndentLevel`
- optional `Stage`
- optional `StepId`
- required `Iteration` for dispatch
- optional `AssertionScope`

`DebuggingRunSession` enriches the entry with the current async step execution context and the current step iteration before forwarding it to `IRunDebugger.SignalLogEntryAsync(...)`.

If no active step iteration exists, `DebuggingRunSession.LogAsync(...)` throws instead of silently routing the entry elsewhere.

### 6a. Structured assertions

Assertions are emitted through `SignalAssertionAsync(...)`.

Each assertion carries:

- `OccurredAtUtc`
- `TargetKind`
- `Target`
- `AssertionName`
- `AssertionDisplay`
- `Succeeded`
- `Expected`
- `Actual`
- `FailureReason`
- `AssertionScope`

### 7. Run completion

After all stages finish, `TimelineRunBuilder` emits:

- `Run: Running -> Complete` if all stages completed
- `Run: Running -> Error` if any stage failed

If execution exits through the `finally` path before that final run transition is emitted, the builder sends:

- `Run: Running -> Error`

Then the session is always closed with:

- `SignalTimelineRunFinishedAsync(sessionId)`

This is the final flush point for the debugger transport.

## Multi-consumer debugger fan-out

`CommonDebugger.GetCommon(...)` now assembles a composite debugger endpoint from three sources:

- any `IRunDebugger` registrations from the current service provider
- the output-helper adapter when an `ITestOutputHelper` is available
- the discovered pipe debugger implementation when the DebugUI adapter assembly is present

`CompositeRunDebugger` forwards every protocol call to every consumer.

Important behavior rules:

- init, transition, value, log, and finish signals are broadcast to every consumer
- breakpoint waits are also broadcast and the run continues only after every consumer returns
- the xUnit/output consumer is passive for breakpoints and interprets lifecycle/value/log signals into text output

This keeps the debugger contract singular from Core's point of view while allowing multiple observers to attach.

## Parallel execution semantics

Parallelism is stage-local. `CoreRunner` first respects authored phase boundaries, then applies IO and explicit barrier rules inside those boundaries, and finally executes each resulting layer with `Task.WhenAll(...)`.

The current built-in phases are:

- `Prepare`: mergeable by default when there is no IO conflict
- `Act`: ordered by default, because side-effecting trigger steps should not silently coalesce
- `Observe`: ordered by default, so polling and wait steps keep their authored sequence
- `Materialize`: mergeable by default when there is no IO conflict

`ExecutionOptions.ParallelizationMode = DoNotParallelize` still exists as an explicit override, but it is no longer the default mechanism that explains why trigger/event/materialization flows serialize.

That means the debugger protocol must not rely on a single global "current step". Instead:

- every transition carries `EntityKind`, `Stage`, and `StepId` when applicable
- every value update carries `ValueKind`, `Stage`, and `StepId` when it can be attributed to a running step
- retry metadata is carried per transition via `OutcomeState`

This is why the protocol behaves like a state machine instead of a sequential text stream.

## Value envelope format

Artifacts and variables use `DebugValueEnvelope`.

The envelope contains:

- `Kind`
- `TypeName`
- `DisplayText`
- `Description`
- `SchemaKey`
- optional `Version`
- `Core`
- `Custom`

A variable and an artifact are both a `DebugValue`: a key and an envelope. They are not separate types,
because nothing downstream can act on a difference the envelope's `Kind` already states.

`Lifecycle` is present on an artifact and absent on a variable, and carries the state and the whole
version history as fields. A variable is its current value and nothing else, so it has no lifecycle to
report — the absence is the statement.

`Description` states what the value is — summary, shape, named fields, badges, a bounded preview, and
a reference to the whole of it when it did not fit. `DisplayText` is the older single line, kept so
consumers built against an earlier package keep working, and filled from the description's summary.
`Core` is the common JSON payload. `Custom` is the artifact- or value-specific extension point.

Current usage:

- variables store their key and JSON value in `Core`
- artifacts store key, type, state, version, reference, and data in `Core`
- artifact describers may override `Describe` to say what their kind of thing actually is, and may add
  `SchemaKey` and `Custom` payloads for richer debug views

## Values too large to send

A preview is bounded, because it rides along with every update. When a value does not fit, it is
written to the run's own output and the envelope carries a reference — path, relative path, size and
content hash — instead of the content.

The files land under `<run output>/<test name>-<id>/values/<key>[.v<n>].<ext>`, where `<run output>` is
`TESTFRAMEWORK_OUTPUT` when set and `./TestFrameworkOutput` otherwise. A build sets that variable to
its staging directory and publishes the folder; the values show up beside the test results with no
further integration.

Deliberately not the debug journal: the journal is a recording the DebugUI keeps for itself, gated on
the UI being installed and pruned behind the user's back, none of which is right for something a build
is meant to publish. One file therefore serves three readers — a person opening it, a build publishing
it, and a UI on the same machine reading it directly rather than asking for it back over a transport.

Rules worth knowing:

- a body exists exactly when the preview was truncated, so a consumer showing a cut value can always
  offer the rest
- unchanged content is not rewritten; changed content becomes `.v2`, `.v3`, and so on
- the directory is created on first use, so a run that assigns nothing large leaves nothing behind
- a failure to write truncates the value rather than failing the run

## Logging interpretation model

Framework execution logging is now interpreted by debugger consumers from state-machine signals.

Current division of responsibility:

- `EntityTransition` drives step-start, retry, and final outcome lines
- `ValueUpdate` drives variable/artifact mutation lines
- `BreakpointHitRequest` drives breakpoint lines
- `LogEntry` carries only freeform in-step log text
- `Assertion` carries structured assertion outcomes with owning step context

That keeps the textual output downstream from the debugger protocol instead of upstream in the runner.

## DebugValueSchemaKey idea

`DebugValueSchemaKey` is the stable decoder key for a debug value envelope.

It is intentionally not the same thing as the CLR type name.

The design goal is:

- `TypeName` tells you what runtime type produced the value
- `SchemaKey` tells a debugger or UI which renderer/inspector contract should be used

That separation matters because different runtime types can still share one debug schema, and one artifact's display contract should remain stable even if the implementation type name changes.

Current usage pattern:

- variables are keyed by shape — `tf.value.scalar`, `.text`, `.binary`, `.collection`, `.dictionary`, `.object`, `.null` — because a variable has no schema beyond its shape; it is whatever a step happened to assign it
- artifacts default to `ArtifactDescriber.DebugValueSchemaKey`, which each shipped kind overrides with a `tf.artifact.*` key
- `Core` holds the common JSON fields every consumer can rely on
- `Custom` holds schema-specific extension data that only the matching renderer needs

For artifact describers, the practical rule is:

- keep `DebugValueSchemaKey` stable when the visual/debug contract is stable
- only change it when a consumer would need a different decoder or inspector layout

That lets DebugUI and other debugger consumers bind to the schema contract instead of binding to arbitrary CLR names.