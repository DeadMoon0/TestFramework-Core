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
    /// <remarks>
    /// Given the whole configuration rather than the entry's own section, because that is what a package's
    /// existing reader takes and there is no reason to make it the shape's problem: a reader that validates
    /// an entry usually wants to name the section it came from, and one that resolves a reference wants to
    /// look at a sibling. <see cref="Section"/> and the identifier say where to look.
    /// </remarks>
    /// <param name="configuration">The run's configuration.</param>
    /// <param name="identifier">Which entry.</param>
    /// <returns>The record.</returns>
    public abstract TConfig Read(IConfiguration configuration, string identifier);

    /// <summary>
    /// The resource values this entry declares.
    /// </summary>
    /// <param name="config">The record.</param>
    /// <returns>The values.</returns>
    public abstract IReadOnlyDictionary<ValueKey, string> Values(TConfig config);

    /// <summary>
    /// The record those values amount to, rebuilt for whoever is asking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other direction of <see cref="Values"/>, and the reason configuration no longer has to reach a
    /// step through a service provider. The run holds what a person declared and what a container produced,
    /// as values; this turns them back into the record a client factory takes. So a step reads its
    /// configuration from the run - which is where run data belongs - and still gets a typed record rather
    /// than a bag of strings.
    /// </para>
    /// <para>
    /// Both directions live on one type on purpose. They have to agree about every value name, and a shape
    /// that writes a name its reader does not read is a value nothing can ever see; keeping them together
    /// makes that a round-trip test rather than a hope.
    /// </para>
    /// <para>
    /// <strong>Values, not the section.</strong> The parsing here is from what the run knows, so it must not
    /// reach for configuration again - by the time this is called a container may have overridden half of
    /// it, and reading the file would quietly undo that.
    /// </para>
    /// <para>
    /// A value a run does not have is a value the record defaults, which is what keeps §5's "a default is
    /// documented where the value is introduced" true: the default stays on the record and this leaves it
    /// alone. A value the record cannot do without and the run does not have is a failure naming it.
    /// </para>
    /// <para>
    /// Virtual only while the packages migrate one resource at a time. It becomes abstract once every shape
    /// implements it, and the transitional state is deliberately loud rather than silently returning an
    /// empty record.
    /// </para>
    /// </remarks>
    /// <param name="values">What the run knows about one resource, by value name, from one viewpoint.</param>
    /// <param name="identifier">Which resource, for failure messages.</param>
    /// <returns>The record.</returns>
    public virtual TConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
        => throw new FrameworkConfigurationException(
            $"'{this.GetType().Name}' cannot rebuild {typeof(TConfig).Name} from the run's values yet.",
            [
                $"Override Read(values, identifier) on {this.GetType().Name} so this resource's configuration can come from the run rather than from a store.",
                "Until then, whatever reads this record has to keep reading the configuration store.",
            ]);

    /// <inheritdoc />
    object IConfigShape.Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return this.Read(configuration, identifier);
    }

    /// <inheritdoc />
    IReadOnlyDictionary<ValueKey, string> IConfigShape.Values(object config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return this.Values((TConfig)config);
    }
}
