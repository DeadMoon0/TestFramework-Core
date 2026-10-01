using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Config.Builder.InstanceBuilder;
using TestFramework.Core.Environment;

namespace TestFramework.Config;

/// <summary>
/// Wraps the Core persistent environment context with configuration-first bootstrap and run configuration layering.
/// </summary>
/// <remarks>
/// The context builds the service provider from the setup's configuration, and the context it wraps owns that
/// provider and disposes it with itself. A flag saying whether to dispose it was a choice no caller could make
/// well: the provider was built here, for the components here, and nothing outside sees it.
/// </remarks>
public sealed class ConfigPersistentEnvironmentContext<TSetup> : IAsyncDisposable
    where TSetup : IConfigPersistentEnvironmentSetup, new()
{
    private readonly ConfigInstance _persistentConfig;
    private readonly PersistentEnvironmentContext<TSetup> _inner;

    private ConfigPersistentEnvironmentContext(ConfigInstance persistentConfig, PersistentEnvironmentContext<TSetup> inner)
    {
        _persistentConfig = persistentConfig;
        _inner = inner;
    }

    /// <summary>
    /// Creates the persistent environment context using the setup's configuration snapshot and awaits its bootstrap.
    /// </summary>
    /// <returns>The bootstrapped context.</returns>
    public static Task<ConfigPersistentEnvironmentContext<TSetup>> CreateAsync()
        => CreateAsync(new TSetup(), CancellationToken.None);

    /// <summary>
    /// Creates the persistent environment context from an explicit setup instance and awaits its bootstrap.
    /// </summary>
    /// <param name="setup">The persistent environment setup.</param>
    /// <returns>The bootstrapped context.</returns>
    public static Task<ConfigPersistentEnvironmentContext<TSetup>> CreateAsync(TSetup setup)
        => CreateAsync(setup, CancellationToken.None);

    /// <summary>
    /// Creates the persistent environment context from an explicit setup instance and awaits its bootstrap,
    /// stopping when asked.
    /// </summary>
    /// <param name="setup">The persistent environment setup.</param>
    /// <param name="cancellationToken">Cancels the bootstrap.</param>
    /// <returns>The bootstrapped context.</returns>
    public static async Task<ConfigPersistentEnvironmentContext<TSetup>> CreateAsync(
        TSetup setup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setup);

        ConfigInstance persistentConfig = setup.CreatePersistentConfig();
        PersistentEnvironmentContext<TSetup> inner = await PersistentEnvironmentContext<TSetup>.CreateAsync(
            setup,
            persistentConfig.BuildServiceProvider(),
            cancellationToken);

        return new ConfigPersistentEnvironmentContext<TSetup>(persistentConfig, inner);
    }

    /// <summary>
    /// Creates a run configuration snapshot identical to the persistent configuration snapshot.
    /// </summary>
    /// <returns>The run configuration.</returns>
    public ConfigInstance CreateRunConfig()
        => _persistentConfig.SetupSubInstance().Build();

    /// <summary>
    /// Creates a run configuration snapshot layered on top of the persistent configuration snapshot.
    /// </summary>
    /// <param name="configure">What the run adds or overrides.</param>
    /// <returns>The run configuration.</returns>
    public ConfigInstance CreateRunConfig(Action<IConfigInstanceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        IConfigInstanceBuilder builder = _persistentConfig.SetupSubInstance();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a fresh environment wrapper that reuses the persistent component slice.
    /// </summary>
    public IEnvironmentProvider CreateEnvironment() => _inner.CreateEnvironment();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
