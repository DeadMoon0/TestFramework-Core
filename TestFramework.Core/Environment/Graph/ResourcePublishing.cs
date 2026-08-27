using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// The channels one set of environment components publishes on, one channel per component.
/// </summary>
/// <remarks>
/// <para>
/// A component is handed a channel rather than the store, so a value's recorded source is the component
/// that produced it rather than "the environment". Naming the channel lives here and only here: two call
/// sites deciding for themselves what a source is called is how a source string turns into something
/// nobody can rely on.
/// </para>
/// <para>
/// It also remembers what each component published, which is what makes a component reusable across runs.
/// A persistent component runs its body once, in a bootstrap, and every later run gets a stand-in that
/// returns the same state - so the addresses that body worked out have to be carried forward the same way
/// the state is. State and produced values are the two things a component makes, and both travel.
/// </para>
/// </remarks>
internal sealed class ResourcePublishing
{
    private readonly ResourceValueStore values;
    private readonly ConcurrentDictionary<EnvComponentIdentifier, EnvironmentResources> channels = new ConcurrentDictionary<EnvComponentIdentifier, EnvironmentResources>();

    internal ResourcePublishing(ResourceValueStore values)
    {
        ArgumentNullException.ThrowIfNull(values);

        this.values = values;
        this.Resolution = new ValueResolution(values);
    }

    /// <summary>
    /// The read side of the same values, for whoever is driving these components.
    /// </summary>
    /// <remarks>
    /// A bootstrap outside any run still needs one: its second component regularly has to know where its
    /// first one ended up, and the alternative - reaching for the state object and asking it - is how a
    /// component ends up depending on another package's internals.
    /// </remarks>
    internal ValueResolution Resolution { get; }

    /// <summary>
    /// The channel belonging to one component. The same component always gets the same channel, because
    /// what it published has to survive its creation call.
    /// </summary>
    /// <param name="component">Which component is about to publish.</param>
    /// <returns>Its channel.</returns>
    internal EnvironmentResources ChannelFor(EnvComponentIdentifier component)
        => this.channels.GetOrAdd(component, id => new EnvironmentResources(this.values, id.ToString()));

    /// <summary>
    /// What one component published here, in the order it published it.
    /// </summary>
    /// <remarks>
    /// Empty for a component that published nothing, and for one that never ran - which are the same
    /// answer to the only question a caller asks: what has to be republished to put a later run in the
    /// same position as this one.
    /// </remarks>
    /// <param name="component">Which component.</param>
    /// <returns>Its values.</returns>
    internal IReadOnlyList<ResolvedValue> PublishedBy(EnvComponentIdentifier component)
        => this.channels.TryGetValue(component, out EnvironmentResources? channel) ? channel.Published : [];
}
