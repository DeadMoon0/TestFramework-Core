using System;

namespace TestFramework.Core.Environment;

/// <summary>
/// Allows an environment provider to expose a run-scoped service provider wrapper.
/// </summary>
public interface IRunScopedServiceProviderFactory
{
    /// <summary>
    /// Creates a service provider used for a specific timeline run.
    /// </summary>
    /// <param name="baseServiceProvider">The base service provider configured for the run.</param>
    /// <returns>A service provider that can resolve additional run-scoped services.</returns>
    IServiceProvider CreateRunScopedServiceProvider(IServiceProvider baseServiceProvider);
}
