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
</key_concepts>

<best_practices>
    Use Simple when the logic is truly lightweight.
    Move repeated or complex behavior into reusable framework code instead of growing large inline actions.
    Keep assertions outside the trigger body where possible.
</best_practices>

<api_hints>
    Important APIs:
    - Simple.Trigger.Action(...)
    - Simple.Trigger.MessageBox(...)

    Source-level behavior:
    ActionTrigger executes a delegate with IServiceProvider, ScopedLogger, resolved variables, and resolved artifacts.
    It declares inputs from referenced variables and artifacts so the timeline still has explicit IO information.
</api_hints>

<style_guide>
    Prefer short delegates with obvious intent.
    If the action body starts to contain branching, repeated logic, or many dependencies, recommend a proper reusable step instead.
    Keep the delegate focused on the action and keep verification in the post-run assertion block.
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

<sources>
    TestFramework-Core/TestFramework.Simple/README.md
    TestFramework-Core/TestFramework.Simple/ActionTrigger.cs
    TestFramework-Core/TestFramework.Simple/MessageBoxTrigger.cs
</sources>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Core/TestFramework.Simple/TestFramework.Simple.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Core/TestFramework.Simple/TestFramework.Simple.csproj -getProperty:PackageProjectUrl
</repo_resolution>