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
    /// <remarks>
    /// The factory keeps ownership: the run never disposes what this returns. A run-scoped provider is
    /// normally a thin layer over <paramref name="baseServiceProvider"/>, which belongs to the caller, and an
    /// implementation may hand the base back unchanged - disposing the result would then dispose the caller's
    /// services. Anything the layer itself creates and must release is the factory's to release.
    /// </remarks>
    /// <param name="baseServiceProvider">The base service provider configured for the run.</param>
    /// <returns>A service provider that can resolve additional run-scoped services.</returns>
    IServiceProvider CreateRunScopedServiceProvider(IServiceProvider baseServiceProvider);
}
