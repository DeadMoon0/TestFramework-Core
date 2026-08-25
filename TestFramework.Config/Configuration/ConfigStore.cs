using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Config.Configuration;

/// <summary>
/// What an author declared, per identifier, for one kind of configuration.
/// </summary>
/// <remarks>
/// <para>
/// One store type for the whole family, and it holds declarations only. It used to double as the channel
/// a starting container published its address through - which is why anything wanting an address had to
/// know somebody else's configuration types, and why two packages ended up with opposite ideas about
/// whether a file or a container won. Addresses now travel as resource values; this holds what a person
/// wrote.
/// </para>
/// <para>
/// Sealed once loading finishes, so "declared" keeps meaning declared. A store that could still be
/// written to at run time is a store nobody can trust to describe intent.
/// </para>
/// </remarks>
/// <typeparam name="TConfig">The configuration record.</typeparam>
public sealed class ConfigStore<TConfig>
{
    private readonly Dictionary<string, TConfig> entries = new Dictionary<string, TConfig>(StringComparer.Ordinal);
    private readonly object syncRoot = new object();

    private bool sealedForRun;

    internal ConfigStore()
    {
    }

    /// <summary>
    /// Creates a store holding one entry, for a test or a fixture that configures by hand.
    /// </summary>
    /// <remarks>
    /// Sealed on the way out: everything a store holds is stated when it is made, which is what lets a
    /// reader trust it to describe intent rather than whatever the run got up to.
    /// </remarks>
    /// <param name="identifier">The identifier.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The store.</returns>
    public static ConfigStore<TConfig> Create(string identifier, TConfig config)
        => Create([new KeyValuePair<string, TConfig>(identifier, config)]);

    /// <summary>
    /// Creates a store holding several entries, for a fixture that configures by hand.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The store, already sealed.</returns>
    public static ConfigStore<TConfig> Create(IEnumerable<KeyValuePair<string, TConfig>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        ConfigStore<TConfig> store = new ConfigStore<TConfig>();

        foreach ((string identifier, TConfig config) in entries)
        {
            store.Add(identifier, config);
        }

        store.Seal();

        return store;
    }

    /// <summary>
    /// Records what an author declared for an identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="config">The configuration.</param>
    /// <exception cref="FrameworkConfigurationException">Loading has finished.</exception>
    internal void Add(string identifier, TConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(config);

        lock (this.syncRoot)
        {
            if (this.sealedForRun)
            {
                throw new FrameworkConfigurationException(
                    $"Configuration for '{identifier}' arrived after loading finished.",
                    [
                        "Declare it while the configuration is being built. Something a run discovers is a resource value, not a declaration.",
                    ],
                    [.. this.entries.Keys.OrderBy(static key => key, StringComparer.Ordinal)]);
            }

            this.entries[identifier] = config;
        }
    }

    /// <summary>
    /// Ends the declaration phase. Called once the configuration has been loaded.
    /// </summary>
    internal void Seal()
    {
        lock (this.syncRoot)
        {
            this.sealedForRun = true;
        }
    }

    /// <summary>
    /// Reads what was declared for an identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="FrameworkConfigurationException">Nothing was declared under that identifier.</exception>
    public TConfig Get(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        lock (this.syncRoot)
        {
            return this.entries.TryGetValue(identifier, out TConfig? config)
                ? config
                : throw new FrameworkConfigurationException(
                    $"Nothing is configured as {typeof(TConfig).Name} under '{identifier}'.",
                    ["Add the entry, or let the environment provide the resource and leave the entry out."],
                    [.. this.entries.Keys.OrderBy(static key => key, StringComparer.Ordinal)]);
        }
    }

    /// <summary>
    /// Reads what was declared, when something was.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="config">The configuration, when there is one.</param>
    /// <returns>True when there is one.</returns>
    public bool TryGet(string identifier, out TConfig? config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        lock (this.syncRoot)
        {
            return this.entries.TryGetValue(identifier, out config);
        }
    }

    /// <summary>
    /// Everything declared, for a listing or a message.
    /// </summary>
    /// <returns>The entries.</returns>
    public IReadOnlyDictionary<string, TConfig> Snapshot()
    {
        lock (this.syncRoot)
        {
            return new Dictionary<string, TConfig>(this.entries, StringComparer.Ordinal);
        }
    }
}
