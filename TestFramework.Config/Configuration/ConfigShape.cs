using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;

namespace TestFramework.Config.Configuration;

/// <summary>
/// How one sort of configuration is read, and which resource values it declares.
/// </summary>
/// <remarks>
/// The successor to a pair of god-interfaces that carried one method per resource type, and stopped
/// spreading for exactly that reason: adding a resource type broke every implementer, so newer packages
/// wrote their own loaders instead. A shape is per config record - a new resource type adds a file.
/// </remarks>
public interface IConfigShape
{
    /// <summary>The configuration section entries live under, for example <c>Api</c>.</summary>
    string Section { get; }

    /// <summary>The kind of resource an entry describes, and therefore which values it may declare.</summary>
    ResourceKind Kind { get; }

    /// <summary>The configuration record this shape reads.</summary>
    Type ConfigType { get; }

    /// <summary>
    /// The identifiers the configuration declares under <see cref="Section"/>.
    /// </summary>
    /// <param name="configuration">The run's configuration.</param>
    /// <returns>The identifiers.</returns>
    IReadOnlyList<string> Identifiers(IConfiguration configuration);

    /// <summary>
    /// Reads one entry.
    /// </summary>
    /// <param name="configuration">The run's configuration.</param>
    /// <param name="identifier">Which entry.</param>
    /// <returns>The configuration record.</returns>
    object Read(IConfiguration configuration, string identifier);

    /// <summary>
    /// The resource values this entry declares - an address a person wrote, a name they chose.
    /// </summary>
    /// <remarks>
    /// Only what the entry actually holds: an API entry without a health path declares no health path,
    /// and that is a normal entry rather than a gap. Every key must be one the <see cref="Kind"/> offers.
    /// </remarks>
    /// <param name="config">The record <see cref="Read"/> returned.</param>
    /// <returns>The values, keyed the way the owning node names them.</returns>
    IReadOnlyDictionary<ValueKey, string> Values(object config);
}

/// <summary>
/// How one sort of configuration is read, typed.
/// </summary>
/// <typeparam name="TConfig">The configuration record.</typeparam>
public abstract class ConfigShape<TConfig> : IConfigShape
    where TConfig : class
{
    /// <inheritdoc />
    public abstract string Section { get; }

    /// <inheritdoc />
    public abstract ResourceKind Kind { get; }

    /// <inheritdoc />
    public Type ConfigType => typeof(TConfig);

    /// <summary>
    /// The identifiers declared under <see cref="Section"/>.
    /// </summary>
    /// <remarks>
    /// Every child of the section, which is how configuration names instances everywhere in the family.
    /// Override only for a section that is shaped differently.
    /// </remarks>
    /// <param name="configuration">The run's configuration.</param>
    /// <returns>The identifiers.</returns>
    public virtual IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return [.. configuration.GetSection(this.Section).GetChildren().Select(static child => child.Key)];
    }

    /// <summary>
    /// Reads one entry.
    /// </summary>
    /// <param name="section">The entry's own section.</param>
    /// <param name="identifier">Which entry, for messages.</param>
    /// <returns>The record.</returns>
    public abstract TConfig Read(IConfigurationSection section, string identifier);

    /// <summary>
    /// The resource values this entry declares.
    /// </summary>
    /// <param name="config">The record.</param>
    /// <returns>The values.</returns>
    public abstract IReadOnlyDictionary<ValueKey, string> Values(TConfig config);

    /// <inheritdoc />
    object IConfigShape.Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return this.Read(configuration.GetSection(this.Section).GetSection(identifier), identifier);
    }

    /// <inheritdoc />
    IReadOnlyDictionary<ValueKey, string> IConfigShape.Values(object config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return this.Values((TConfig)config);
    }
}
