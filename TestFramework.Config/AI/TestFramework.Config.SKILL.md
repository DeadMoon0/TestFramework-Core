<identity>
    <package>TestFramework.Config</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain how TestFramework.Config prepares IConfiguration and IServiceProvider for timeline runs.
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
</key_concepts>

<best_practices>
    Keep a shared base configuration and derive per-test variants from it.
    Prefer explicit overrides over hidden environment assumptions.
    Use Config for setup concerns, not for test assertions.
    Keep configuration mutation close to the test that needs it.
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

<style_guide>
    Prefer one shared base config per fixture or class when several tests differ only by a few values.
    Use override keys that mirror the final configuration path exactly.
    Do not hide important test inputs inside unrelated service-registration helpers.
</style_guide>

<sample_patterns>
    Shared base plus variants:
    - Build one ConfigInstance from JSON.
    - Call SetupSubInstance() per scenario.
    - Override only the values that are scenario-specific.

    Timeline integration:
    - BuildServiceProvider() and pass the result into SetupRun(provider).
    - Keep service-provider creation outside the assertion section.
</sample_patterns>

<sources>
    TestFramework-Core/TestFramework.Config/README.md
    TestFramework-Core/TestFramework.Config/ConfigInstance.cs
    TestFramework-Core/Documentation/TestFramework.Config.Documentation.md
</sources>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-Core/TestFramework.Config/TestFramework.Config.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-Core/TestFramework.Config/TestFramework.Config.csproj -getProperty:PackageProjectUrl
</repo_resolution>