<identity>
    <package>TestFramework.Core</package>
    <role>base-skill</role>
</identity>

<objective>
    Provide the reusable framework knowledge for the TestFramework agent.
    Explain the timeline model, extension seams, assertion style, API vocabulary, and test-writing conventions shared across all addon skills.
</objective>

<package_scope>
    Covers framework-wide concepts from TestFramework.Core: timelines, steps, variables, artifacts, assertions, logging, debugging, and extension patterns.
</package_scope>

<architecture>
    The framework uses a readable timeline model: build a timeline, create a run, execute with RunAsync, then assert on the immutable run result.
    Other packages extend this engine with package-specific triggers, events, artifacts, configuration, or helper APIs.
    The base mental model is not "call helper methods until the test passes".
    The base mental model is "describe a run as a readable timeline, then inspect the resulting run object".
</architecture>

<best_practices>
    Prefer short, readable, vertically structured tests.
    Use named steps instead of positional indexing.
    Keep reusable timeline structure at class scope when multiple tests share it.
    Keep shared conventions in this Core skill and package-specific knowledge in addon skills.
    Prefer one obvious flow over deeply nested helper abstractions.
    Keep setup, execution, and assertions conceptually separate even when they live in one short test method.
</best_practices>

<test_conventions>
    Preferred structure:
    class level static Timeline
    standard async unit-test method
    SetupRun(...).RunAsync()
    run.EnsureRanToCompletion()
    assertions on run state, named steps, variables, and artifacts
</test_conventions>

<style_guide>
    Canonical shape:
    1. Build the timeline once, usually as a class field.
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
</api_hints>

<sample_patterns>
    Minimal pattern from the showroom:
    - Build a timeline once.
    - In the test body call SetupRun().RunAsync().
    - Immediately call run.EnsureRanToCompletion().

    Assertion-focused pattern from the showroom:
    - Name important steps.
    - Assert with run.Step("name").Should().HaveCompleted().
    - Use run.AssertionScope() when several failures should be reported together.

    Variable-driven pattern:
    - Use variables for values that differ per run.
    - Keep the timeline readable by making the variable names communicate intent.
</sample_patterns>

<decision_rules>
    Recommend Simple only when the behavior is small and local.
    Recommend Config when runtime services or configuration must be injected cleanly.
    Recommend addon packages when the test interacts with external systems instead of inventing custom plumbing in Core.
    Prefer fluent run assertions over ad-hoc debugging code for normal test verification.
</decision_rules>

<sources>
    TestFramework-Core/Documentation/Arc42.md
    TestFramework-Core/TestFramework.Core/README.md
    TestFramework-Showroom/README.md
    TestFramework-Showroom/TestFramework.Showroom.Basic
</sources>

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