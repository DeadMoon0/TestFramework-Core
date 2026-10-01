using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// An identity for a test that signals a debugger by hand, where only the run's name and project matter.
/// </summary>
internal static class DebugTestIdentity
{
    public static TestIdentity For(string name, string projectPath)
        => new() { DisplayName = name, AssemblyPath = projectPath, Framework = TestFrameworkKind.Unknown };
}
