using TestFramework.Core.Environment;

namespace TestFramework.Config;

/// <summary>
/// Extends a persistent environment setup with the configuration snapshot used for persistent bootstrap and run layering.
/// </summary>
public interface IConfigPersistentEnvironmentSetup : IPersistentEnvironmentSetup
{
    /// <summary>
    /// Creates the configuration snapshot used to bootstrap persistent services and derive later run configuration.
    /// </summary>
    ConfigInstance CreatePersistentConfig();
}