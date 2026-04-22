<identity>
    <package>TestFramework.Simple</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the lightweight helper triggers in TestFramework.Simple for cases where a full custom Step type is unnecessary.
</objective>

<package_scope>
    Covers inline action triggers and simple helper flows.
</package_scope>

<key_concepts>
    Use Simple.Trigger.Action(...) for inline actions.
    Use variable-aware action overloads when timeline values need to flow into the delegate.
    Keep Simple-based tests small and obvious.
    Simple is a convenience layer, not a substitute for good reusable abstractions when behavior grows.
    ActionTrigger is still a real step in the Core pipeline, with tracked inputs and cloned options.
</key_concepts>

<best_practices>
    Use Simple when the logic is truly lightweight.
    Move repeated or complex behavior into reusable framework code instead of growing large inline actions.
    Keep assertions outside the trigger body where possible.
    Prefer Simple for test-local side effects, tiny inline logging, or very small orchestration glue.
</best_practices>

<api_hints>
    Important APIs:
    - Simple.Trigger.Action(...)
    - Simple.Trigger.MessageBox(...)

    Source-level behavior:
    ActionTrigger executes a delegate with IServiceProvider, ScopedLogger, resolved variables, and resolved artifacts.
    It declares inputs from referenced variables and artifacts so the timeline still has explicit IO information.
</api_hints>

<runtime_behavior>
    Important runtime facts:
    - ActionTrigger executes synchronously and returns null as its result.
    - Referenced variables and artifacts are resolved before the delegate is called.
    - DeclareIO() reflects the referenced variables and artifacts as required inputs, so the timeline still validates properly.
    - MessageBoxTrigger blocks until the user dismisses the message box and is therefore a debugging or demo tool, not a normal production test primitive.
</runtime_behavior>

<style_guide>
    Prefer short delegates with obvious intent.
    If the action body starts to contain branching, repeated logic, or many dependencies, recommend a proper reusable step instead.
    Keep the delegate focused on the action and keep verification in the post-run assertion block.
    Prefer one tiny delegate with a clear side effect over a delegate that silently contains business workflow logic.
</style_guide>

<sample_patterns>
    Good Simple usage:
    - one inline action that sets a value, writes a log line, or triggers a tiny side effect
    - one message-box or lightweight local interaction used for demonstration or debugging

    Bad Simple usage:
    - large business logic inside the delegate
    - hidden assertions inside the action body
    - repeated copied delegates across many tests
</sample_patterns>

<decision_rules>
    Recommend Simple when:
    - the user needs a tiny inline action
    - the behavior is local to one test
    - creating a custom Step<T> would add more ceremony than value

    Recommend a custom step instead when:
    - the logic needs reuse
    - the logic is async-heavy or structurally complex
    - the action body starts owning real domain logic
</decision_rules>

<anti_patterns>
    Avoid:
    - embedding assertions in the action delegate
    - copying large delegate bodies across several tests
    - using Simple as the main abstraction for a growing workflow
</anti_patterns>

<sources>
    TestFramework-Core/TestFramework.Simple/README.md
    TestFramework-Core/TestFramework.Simple/ActionTrigger.cs
    TestFramework-Core/TestFramework.Simple/MessageBoxTrigger.cs
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-Core/TestFramework.Simple/Simple.cs
    - TestFramework-Core/TestFramework.Simple/ActionTrigger.cs
    - TestFramework-Core/TestFramework.Simple/MessageBoxTrigger.cs
    - TestFramework-Core/UnitTests/TestFramework.Simple.Tests/ActionTriggerTests.cs
    - TestFramework-Core/UnitTests/TestFramework.Simple.Tests/MessageBoxTriggerTests.cs
</grounding_files>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Core/TestFramework.Simple/TestFramework.Simple.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Core/TestFramework.Simple/TestFramework.Simple.csproj -getProperty:PackageProjectUrl
</repo_resolution>