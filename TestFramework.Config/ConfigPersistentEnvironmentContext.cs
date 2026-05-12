using System;
using System.Threading.Tasks;
using TestFramework.Config.Builder.InstanceBuilder;
using TestFramework.Core.Environment;

namespace TestFramework.Config;

/// <summary>
/// Wraps the Core persistent environment context with configuration-first bootstrap and run configuration layering.
/// </summary>
public sealed class ConfigPersistentEnvironmentContext<TSetup> : IAsyncDisposable
    where TSetup : IConfigPersistentEnvironmentSetup, new()
{
    private readonly ConfigInstance _persistentConfig;
    private readonly PersistentEnvironmentContext<TSetup> _inner;

    /// <summary>
    /// Creates and bootstraps the persistent environment context using the setup's configuration snapshot.
    /// </summary>
    public ConfigPersistentEnvironmentContext(bool disposePersistentServiceProvider = true)
        : this(new TSetup(), disposePersistentServiceProvider)
    {
    }

    /// <summary>
    /// Creates and bootstraps the persistent environment context using an explicit setup instance.
    /// </summary>
    public ConfigPersistentEnvironmentContext(TSetup setup, bool disposePersistentServiceProvider = true)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _persistentConfig = setup.CreatePersistentConfig();
        _inner = new PersistentEnvironmentContext<TSetup>(setup, _persistentConfig.BuildServiceProvider(), disposePersistentServiceProvider);
    }

    /// <summary>
    /// Creates a run configuration snapshot layered on top of the persistent configuration snapshot.
    /// </summary>
    public ConfigInstance CreateRunConfig(Action<IConfigInstanceBuilder>? configure = null)
    {
        IConfigInstanceBuilder builder = _persistentConfig.SetupSubInstance();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a fresh environment wrapper that reuses the persistent component slice.
    /// </summary>
    public IEnvironmentProvider CreateEnvironment() => _inner.CreateEnvironment();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}