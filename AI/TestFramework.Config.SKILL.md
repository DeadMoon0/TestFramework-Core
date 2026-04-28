<identity>
    <package>TestFramework.Config</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain how TestFramework.Config prepares IConfiguration and IServiceProvider for timeline runs through layered configuration deltas and service-registration inheritance.
</objective>

<package_scope>
    Covers ConfigInstance creation, JSON-backed configuration, overrides, service registration, and reusable sub-instances for tests.
</package_scope>

<key_concepts>
    Use ConfigInstance.FromJsonFile(...) when tests start from configuration files.
    Use OverrideConfig(...) for test-specific values.
    Use AddService(...) to register dependencies required by steps.
    Use BuildServiceProvider() when SetupRun(...) needs an IServiceProvider.
    SetupSubInstance() is the preferred way to derive test-specific variants from a shared base configuration.
    Think in layers, not mutation: a base ConfigInstance collects deltas, and child instances add their own deltas on top.
</key_concepts>

<best_practices>
    Keep a shared base configuration and derive per-test variants from it.
    Prefer explicit overrides over hidden environment assumptions.
    Use Config for setup concerns, not for test assertions.
    Keep configuration mutation close to the test that needs it.
    Load JSON once and derive multiple scenario-specific providers from the built base instance.
    Prefer documenting or asserting the final override path clearly instead of hiding it in helper code.
    Keep config examples compact so the reader can still see the scenario, not only the wiring.
</best_practices>

<api_hints>
    Important APIs:
    - ConfigInstance.FromJsonFile(path)
    - ConfigInstance.Create()
    - SetupSubInstance()
    - OverrideConfig(key, value)
    - AddService((services, configuration) => ...)
    - Build()
    - BuildServiceProvider()

    Implementation detail worth knowing:
    ConfigInstance materializes IConfiguration from collected deltas and builds a ServiceCollection before returning IServiceProvider.
</api_hints>

<mental_model>
    TestFramework.Config is a delta-layered configuration system.
    A ConfigInstance does not encourage global mutable state. Instead it captures configuration and service-registration changes as layers, then materializes the final IConfiguration and IServiceProvider only when Build() or BuildServiceProvider() is called.
</mental_model>

<runtime_behavior>
    Materialization flow:
    - create a base instance from JSON or from Create()
    - add overrides and service-registration actions
    - optionally call SetupSubInstance() to derive children
    - build the final provider for the current scenario

    Important runtime facts:
    - child overrides win over parent values
    - service registrations see the merged IConfiguration at materialization time
    - IConfiguration is registered into the provider automatically
    - null values from imported JSON are skipped
    - missing files, invalid JSON, and registration failures should be treated as public setup-contract errors, not as vague downstream runtime surprises
</runtime_behavior>

<usage_guidance>
    Overwrite precedence mental model:
    - base JSON or Create() establishes the root layer
    - parent overrides apply before child overrides
    - SetupSubInstance() is the preferred branching point for scenario variants
    - the final Build()/BuildServiceProvider() materialization is where merged config and service registration actually become concrete

    Agent recommendation:
    - when a user wants project-wide test configuration reuse, propose one shared base ConfigInstance plus scenario-specific sub-instances
    - when a failure looks like a missing service, inspect AddService(...) and the final merged key paths before changing timeline code
</usage_guidance>

<style_guide>
    Prefer one shared base config per fixture or class when several tests differ only by a few values.
    Use override keys that mirror the final configuration path exactly.
    Do not hide important test inputs inside unrelated service-registration helpers.
    Keep the configuration section short enough that a reader can still see what is unique about the test.
    Prefer ordinary C# constants or locals for one-off values instead of inventing extra framework-level naming.
    Keep `SetupRun(provider).RunAsync()` examples compact when no extra explanation is needed.
</style_guide>

<sample_patterns>
    Shared base plus variants:
    - Build one ConfigInstance from JSON.
    - Call SetupSubInstance() per scenario.
    - Override only the values that are scenario-specific.

    Timeline integration:
    - BuildServiceProvider() and pass the result into SetupRun(provider).
    - Keep service-provider creation outside the assertion section.

    Service-with-config pattern:
    - Override config values first.
    - Register services with AddService((services, configuration) => ...).
    - Let the final provider resolve the merged configuration for the registration.
</sample_patterns>

<anti_patterns>
    Avoid:
    - reloading the same JSON configuration for every test when a shared base instance is enough
    - hiding important per-test overrides inside opaque setup helpers
    - treating Config as the place for assertions or business verification
    - guessing config paths instead of using the real final key path structure
</anti_patterns>

<important_type_map>
    Common type map for discovery and error interpretation:
    - ConfigInstance: layered config builder and root entry point for this package
    - IConfigInstanceBuilder: fluent builder interface used before Build() materializes the instance
    - ConfigServiceDeltaCollection: ordered set of configuration and service-registration deltas
    - IConfiguration: merged configuration view made available during materialization and in DI
    - IServiceProvider: built provider typically passed into SetupRun(...)

    Discovery heuristics for the agent:
    - If users talk about test settings files, overrides, or shared config roots, they usually mean ConfigInstance.
    - If errors mention missing services after BuildServiceProvider(), inspect AddService(...) registrations and the merged IConfiguration.
    - If users describe parent-child config variants, they usually need SetupSubInstance().
</important_type_map>

<sources>
    TestFramework-Core/TestFramework.Config/README.md
    TestFramework-Core/TestFramework.Config/ConfigInstance.cs
    TestFramework-Core/Documentation/TestFramework.Config.Documentation.md
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-Core/TestFramework.Config/ConfigInstance.cs
    - TestFramework-Core/TestFramework.Config/ConfigServiceDeltaCollection.cs
    - TestFramework-Core/TestFramework.Config/Builder/ConfigInstanceBuilder.cs
    - TestFramework-Core/UnitTests/TestFramework.Config.Tests/ConfigInstanceTests.cs
    - TestFramework-Core/UnitTests/TestFramework.Config.Tests/ConfigServiceDeltaCollectionTests.cs
</grounding_files>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Core/TestFramework.Config/TestFramework.Config.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Core/TestFramework.Config/TestFramework.Config.csproj -getProperty:PackageProjectUrl
</repo_resolution>