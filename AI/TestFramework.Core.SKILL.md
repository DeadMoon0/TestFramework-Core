<identity>
    <package>TestFramework.Core</package>
    <role>base-skill</role>
</identity>

<objective>
    Provide the reusable framework knowledge for the TestFramework agent.
    Explain the timeline model, execution pipeline, assertion style, extension seams, API vocabulary, and test-writing conventions shared across all addon skills.
</objective>

<package_scope>
    Covers framework-wide concepts from TestFramework.Core: timelines, steps, variables, artifacts, assertions, logging, debugging, and extension patterns.
</package_scope>

<architecture>
    The framework uses a readable timeline model: build a timeline, create a run, execute with RunAsync, then assert on the immutable run result.
    Other packages extend this engine with package-specific triggers, events, artifacts, configuration, or helper APIs.
    The base mental model is not "call helper methods until the test passes".
    The base mental model is "describe a run as a readable timeline, then inspect the resulting run object".
    Build, run, and result are intentionally separated. The timeline is the reusable template, SetupRun(...) creates the runtime instance, and TimelineRun is the frozen result snapshot.
</architecture>

<public_surface_model>
    For 1.0 reasoning, treat the public Core surface as three layers:
    - consumer-first API: Timeline.Create() -> Build() -> SetupRun(...) -> RunAsync(), TimelineRun, StepHandle, Var helpers, and fluent assertions
    - advanced extension API: artifacts, environment providers, events, logging, and selected runtime abstractions used by addon packages
    - compatibility-preserving scaffolding: builder action interfaces, debugger seams, and preprocessors that remain public but should not be presented as the normal learning path

    Builder action interfaces, debugger records, and preprocessor emitters are intentionally de-emphasized in IntelliSense.
    Do not route ordinary test-authoring guidance through those types unless the user is explicitly extending the framework itself.
</public_surface_model>

<best_practices>
    Prefer short, readable, vertically structured tests.
    Use named steps instead of positional indexing.
    Keep reusable timeline structure at class scope when multiple tests share it.
    Keep shared conventions in this Core skill and package-specific knowledge in addon skills.
    Prefer one obvious flow over deeply nested helper abstractions.
    Keep setup, execution, and assertions conceptually separate even when they live in one short test method.
    Prefer data flow through variables and artifacts over ad-hoc mutable local state.
    Prefer extension packages when the test touches a real external system instead of inventing custom plumbing in Core.
    Strongly prefer the existing global TestFramework building blocks over custom Steps, custom Artifacts, or custom framework primitives.
</best_practices>

<test_conventions>
    Preferred structure:
    private static readonly Timeline _timeline
    standard async unit-test method
    SetupRun(...).RunAsync()
    run.EnsureRanToCompletion()
    assertions on run state, named steps, variables, and artifacts
</test_conventions>

<structure_rules>
    Showroom-driven structure rules:
    - Put the timeline on the test class as private static readonly Timeline _timeline when the timeline shape is shared by the test methods.
    - Keep interaction code inside the timeline whenever the interaction is part of the scenario under test.
    - Outside the timeline, only prepare run-local input values, build providers/config, start the run, and assert on the result.
    - Do not hide remote calls, file operations, message sends, or other scenario interactions in imperative code before or after the run if the timeline can model them.
    - Keep the timeline vertically readable: builder actions start on their own indentation level, modifiers such as Name(...), WithTimeOut(...), and WithRetry(...) continue directly under the step they modify.

    Visual shape to prefer:
    - one builder action per line
    - chained modifiers indented one level deeper than the action they modify
    - Build() as the final line of the field initializer
</structure_rules>

<style_guide>
    Canonical shape:
    1. Build the timeline once, usually as a private static readonly class field.
    2. Keep the test method focused on runtime input, execution, and assertions.
    3. Read the test top to bottom like a short script.

    Naming rules:
    - Prefer descriptive test names that communicate the scenario.
    - Prefer named steps when later assertions or diagnostics need to refer to them.
    - Prefer stable variable names instead of abbreviations.

    Readability rules:
    - Avoid positional assertions like run.Steps()[0] when a named step is possible.
    - Avoid hiding the core timeline flow inside large helper methods.
    - Avoid mixing environment setup logic into assertion sections.
    - Prefer timeline formatting where the action is the anchor line and its modifiers are indented below it.
    - Prefer run-local data setup outside the timeline, but keep scenario interaction steps inside the timeline.
</style_guide>

<api_hints>
    Core entry points the agent should recognize:
    - Timeline.Create() starts the builder flow.
    - Build() produces a reusable Timeline.
    - SetupRun(...) creates a run builder, optionally with services or test output.
    - RunAsync() executes the timeline.
    - EnsureRanToCompletion() is the default post-run success guard.
    - run.Step(name), run.Steps(name), run.Variable<T>(name), and run.AssertionScope() are important assertion surfaces.
    - Var.Const(...) and Var.Ref<T>(...) express static versus runtime-resolved values.

    Builder actions the agent should recognize:
    - Trigger(step)
    - SetVariable(identifier, value)
    - Transform(...)
    - AssertVariable(...)
    - WaitForEvent(...)
    - SetupArtifact(...), RegisterArtifact(...), RemoveArtifact(...)
    - Conditional(...)
    - ForEach(...)

    Step modifiers the agent should recognize:
    - Name(string) //Use only when nessesary - dont Spam
    - WithTimeOut(...)
    - WithRetry(...)
    - ExpectExceptions(...)
</api_hints>

<runtime_behavior>
    Execution pipeline:
    - The built Timeline is reused.
    - SetupRun(...) creates a per-run builder.
    - RunAsync() preprocesses and validates the run, then executes steps and cleanup.
    - The final TimelineRun is frozen and intended for inspection, not mutation.

    Variable model:
    - Var.Const(value) is static.
    - Var.Ref<T>(identifier) is runtime-resolved from the VariableStore.
    - RefImmutable should be used when control flow depends on values that must not be changed later in the run.

    Artifact model:
    - Artifacts are first-class tracked objects with describers, data, and references.
    - Core expects explicit artifact lifecycle operations instead of hidden file or database handling.
</runtime_behavior>

<sample_patterns>
    Minimal pattern from the showroom:
    - Build a timeline once.
    - In the test body call SetupRun().RunAsync().
    - Immediately call run.EnsureRanToCompletion().

    Static field pattern from the showroom:
    - declare private static readonly Timeline _timeline = Timeline.Create() ... .Build();
    - keep the test body free of direct interaction logic beyond per-run inputs and run execution

    Assertion-focused pattern from the showroom:
    - Name important steps.
    - Assert with run.Step("name").Should().HaveCompleted().
    - Use run.AssertionScope() when several failures should be reported together.

    Variable-driven pattern:
    - Use variables for values that differ per run.
    - Keep the timeline readable by making the variable names communicate intent.

    Extension pattern:
    - Use Core for the test shape and flow.
    - Add package-specific triggers, events, artifacts, or config only where the scenario actually needs them.
</sample_patterns>

<decision_rules>
    Recommend Simple only when the behavior is small and local.
    Recommend Config when runtime services or configuration must be injected cleanly.
    Recommend addon packages when the test interacts with external systems instead of inventing custom plumbing in Core.
    Prefer fluent run assertions over ad-hoc debugging code for normal test verification.
    Discourage creating custom Step, Artifact, Event, or other framework component types unless the user explicitly asks for framework extension work and no existing package primitive can model the scenario.
    Prefer composing existing Timeline actions, variables, events, config integration, and package entry points over adding new reusable framework components.
    When users discover builder-action interfaces, debugger types, or preprocessors directly, translate them back into the higher-level consumer surface before proposing changes.
</decision_rules>

<release_readiness_notes>
    1.0 grounding the agent should preserve:
    - package XML documentation generation is enabled and the current public surface is documented
    - the consumer-first contract is the intended documentation anchor even though broader advanced surfaces remain public
    - future cleanup candidates exist, but they belong to a later breaking-change window, not late 1.0 reshaping

    Practical recommendation:
    - for normal usage, keep guidance centered on timelines, run setup, variables, assertions, and addon-package entry points
    - for extension work, explicitly say when the user is stepping from consumer API into extension API
</release_readiness_notes>

<anti_patterns>
    Avoid:
    - positional assertions like run.Steps()[0] when named steps are possible
    - hiding the core timeline flow inside large helper methods
    - using mutable local control flow when variables communicate intent better
    - mixing environment setup logic into the assertion section
    - reimplementing package-level capabilities in custom Core-only code when an addon already models the interaction
    - putting real scenario interactions outside the timeline when they should be visible as triggers, waits, transforms, or artifact operations
    - using inconsistent indentation that makes it unclear which modifiers belong to which step
    - creating custom Steps, Artifacts, Events, or similar framework primitives for ordinary test authoring
    - solving test-level problems by extending the framework when existing global components already provide the needed structure
</anti_patterns>

<important_type_map>
    Common type map for discovery and error interpretation:
    - Timeline: reusable test structure; usually the private static readonly _timeline field
    - TimelineRun: immutable result object returned by RunAsync()
    - Step / Step<T>: executable timeline unit; triggers and assertions build on this abstraction
    - Event: asynchronous wait boundary used with WaitForEvent(...)
    - VariableReference<T>: runtime value reference used through Var.Ref<T>(...)
    - VariableStore: per-run storage that resolves variable references
    - Artifact / ArtifactReference / ArtifactData: tracked runtime objects and their data snapshots
    - ScopedLogger: step-scoped logger commonly available inside custom steps and simple actions

    Discovery heuristics for the agent:
    - If users talk about "the timeline", "builder chain", or "_timeline", they usually mean Timeline.Create() ... Build().
    - If users talk about "the run", "result", or completion state, they usually mean TimelineRun.
    - If users talk about named operations, retries, timeouts, or modifiers, they usually mean Step plus chained modifiers.
    - If users talk about refs, runtime values, or transform chains, they usually mean VariableReference<T> and Var helpers.
    - If users talk about captured files, database rows, blobs, or stored entities, they usually mean artifacts and artifact references.
</important_type_map>

<sources>
    TestFramework-Core/Documentation/Arc42.md
    TestFramework-Core/Documentation/CoreArchitecture.md
    TestFramework-Core/TestFramework.Core/README.md
    TestFramework-Showroom/README.md
    TestFramework-Showroom/TestFramework.Showroom.Basic
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-Core/TestFramework.Core/Timelines/Timeline.cs
    - TestFramework-Core/TestFramework.Core/Variables/Var.cs
    - TestFramework-Core/TestFramework.Core/Steps/Step.cs
    - TestFramework-Core/TestFramework.Core/Events/Event.cs
    - TestFramework-Core/Documentation/CoreArchitecture.md
    - TestFramework-Showroom/TestFramework.Showroom.Basic
</grounding_files>

<repo_resolution>
    Do not assume or hardcode repository URLs.
    Resolve them when needed with commands such as:
    dotnet msbuild TestFramework-Core/TestFramework.Core/TestFramework.Core.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Core/TestFramework.Core/TestFramework.Core.csproj -getProperty:PackageProjectUrl
</repo_resolution>

<merge>
    This is the base skill.
    Addon skills should extend this knowledge with package-specific APIs, setup requirements, caveats, and examples.
</merge>