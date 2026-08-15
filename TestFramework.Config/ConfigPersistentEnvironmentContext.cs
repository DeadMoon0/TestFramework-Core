using System;
using System.Threading;
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

    private const string BlockingConstructorObsoleteMessage =
        "This constructor blocks the calling thread for the entire bootstrap and deadlocks under a SynchronizationContext. Use ConfigPersistentEnvironmentContext<TSetup>.CreateAsync(...) instead.";

    /// <summary>
    /// Creates and bootstraps the persistent environment context using the setup's configuration snapshot.
    /// </summary>
    [Obsolete(BlockingConstructorObsoleteMessage)]
    public ConfigPersistentEnvironmentContext(bool disposePersistentServiceProvider = true)
        : this(new TSetup(), disposePersistentServiceProvider)
    {
    }

    /// <summary>
    /// Creates and bootstraps the persistent environment context using an explicit setup instance.
    /// </summary>
    [Obsolete(BlockingConstructorObsoleteMessage)]
    public ConfigPersistentEnvironmentContext(TSetup setup, bool disposePersistentServiceProvider = true)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _persistentConfig = setup.CreatePersistentConfig();
        _inner = new PersistentEnvironmentContext<TSetup>(setup, _persistentConfig.BuildServiceProvider(), disposePersistentServiceProvider);
    }

    private ConfigPersistentEnvironmentContext(ConfigInstance persistentConfig, PersistentEnvironmentContext<TSetup> inner)
    {
        _persistentConfig = persistentConfig;
        _inner = inner;
    }

    /// <summary>
    /// Creates the persistent environment context using the setup's configuration snapshot and awaits its bootstrap.
    /// </summary>
    /// <param name="disposePersistentServiceProvider">Whether disposing this context also disposes the configuration's service provider.</param>
    /// <param name="cancellationToken">Cancels the bootstrap.</param>
    public static Task<ConfigPersistentEnvironmentContext<TSetup>> CreateAsync(
        bool disposePersistentServiceProvider = true,
        CancellationToken cancellationToken = default)
        => CreateAsync(new TSetup(), disposePersistentServiceProvider, cancellationToken);

    /// <summary>
    /// Creates the persistent environment context from an explicit setup instance and awaits its bootstrap.
    /// </summary>
    /// <param name="setup">The persistent environment setup.</param>
    /// <param name="disposePersistentServiceProvider">Whether disposing this context also disposes the configuration's service provider.</param>
    /// <param name="cancellationToken">Cancels the bootstrap.</param>
    public static async Task<ConfigPersistentEnvironmentContext<TSetup>> CreateAsync(
        TSetup setup,
        bool disposePersistentServiceProvider = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);

        ConfigInstance persistentConfig = setup.CreatePersistentConfig();
        PersistentEnvironmentContext<TSetup> inner = await PersistentEnvironmentContext<TSetup>.CreateAsync(
            setup,
            persistentConfig.BuildServiceProvider(),
            disposePersistentServiceProvider,
            cancellationToken);

        return new ConfigPersistentEnvironmentContext<TSetup>(persistentConfig, inner);
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