using System;
using System.Diagnostics;

namespace TestFramework.Core;

/// <summary>
/// A no-op <see cref="IServiceProvider"/> for use in tests that do not require dependency injection.
/// Every <see cref="GetService"/> call returns <see langword="null"/>.
/// <para>
/// A <see cref="Debug"/> warning is written whenever a service is requested so that missing
/// registrations are visible during development.  Use
/// <c>ConfigInstance.BuildServiceProvider()</c> from <c>TestFrameworkConfig</c> for real
/// service resolution.
/// </para>
/// </summary>
public class EmptyServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        Debug.WriteLine(
            $"[EmptyServiceProvider] '{serviceType.FullName}' was requested but no IServiceProvider was configured. " +
            "Use ConfigInstance.BuildServiceProvider() to supply services.");
        return null;
    }
}
