using System;
using System.Collections.Generic;

namespace TestFramework.Core.Environment;

/// <summary>
/// Declares the environment instance and persistent component roots owned by a persistent environment context.
/// </summary>
public interface IPersistentEnvironmentSetup
{
    /// <summary>
    /// Creates a fresh environment provider instance.
    /// </summary>
    IEnvironmentProvider CreateEnvironment();

    /// <summary>
    /// Gets the component identifiers that should be created and owned by the persistent context.
    /// </summary>
    IReadOnlyCollection<EnvComponentIdentifier> GetPersistentComponentIdentifiers();

    /// <summary>
    /// Gets the maximum time allowed to bootstrap the persistent component slice.
    /// </summary>
    TimeSpan GetPersistentSetupTimeout() => TimeSpan.FromMinutes(2);
}