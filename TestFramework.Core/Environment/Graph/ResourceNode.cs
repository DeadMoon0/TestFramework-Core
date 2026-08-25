using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// One resource a run knows about: what it is, what it borrows, and what it holds.
/// </summary>
/// <remarks>
/// <para>
/// A node covers both sorts of resource with one shape. Something the run starts - a container, an
/// emulator - is a <see cref="ProvisionedResourceNode"/> and publishes its values when it comes up.
/// Something an author merely declared is a plain node that relays what the configuration says. A
/// consumer cannot tell the difference, and that is the point: a test that talks to a containerized API
/// and a test that talks to a deployed one are the same test.
/// </para>
/// <para>
/// Authoring one is small on purpose - a kind, an identifier, and whichever of the four optional members
/// apply. Notably absent is a dependency list: <see cref="Connections"/> is derived from what the node
/// borrows, so wiring and ordering cannot drift apart.
/// </para>
/// </remarks>
public abstract class ResourceNode
{
    private IReadOnlyList<Connection>? connections;

    /// <summary>What sort of resource this is, and therefore which values it may offer.</summary>
    public abstract ResourceKind Kind { get; }

    /// <summary>Which resource of that kind, as tests and configuration name it.</summary>
    public abstract string Identifier { get; }

    /// <summary>The kind's name, which is what the graph and the run's values are keyed on.</summary>
    public string KindName => this.Kind.Name;

    /// <summary>Where this node is in the graph.</summary>
    public ResourceAddress Address => new ResourceAddress(this.KindName, this.Identifier);

    /// <summary>
    /// What this node borrows from other resources, and where each borrowed value lands.
    /// </summary>
    public virtual IReadOnlyList<ValueRoute> Routes => [];

    /// <summary>
    /// Resources this node needs to exist without borrowing a value from them - a container and its
    /// network.
    /// </summary>
    public virtual IReadOnlyList<ResourceAddress> Ordering => [];

    /// <summary>
    /// The configuration documents this node generates once its routes are resolved.
    /// </summary>
    public virtual IReadOnlyList<ConfigDocument> Documents => [];

    /// <summary>
    /// Values this node has without anything being started - an address an author wrote, a name a
    /// definition chose.
    /// </summary>
    public virtual IReadOnlyDictionary<ValueKey, string> DeclaredValues => ReadOnlyDictionary<ValueKey, string>.Empty;

    /// <summary>
    /// Everything this node needs, grouped by which resource it needs it from.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Routes"/> and <see cref="Ordering"/> rather than declared, so one statement
    /// yields the wiring, the dependency and the creation order together.
    /// </remarks>
    public IReadOnlyList<Connection> Connections => this.connections ??= this.BuildConnections();

    /// <summary>
    /// Reads as <c>web.site/storefront</c>.
    /// </summary>
    /// <returns>The description, used in messages, logs and the run's graph snapshot.</returns>
    public override string ToString() => this.Address.ToString();

    private IReadOnlyList<Connection> BuildConnections()
    {
        Dictionary<ResourceAddress, List<ValueRoute>> grouped = [];

        foreach (ValueRoute route in this.Routes)
        {
            ResourceAddress address = new ResourceAddress(route.Value.ResourceKind!, route.Value.Identifier);

            if (!grouped.TryGetValue(address, out List<ValueRoute>? routes))
            {
                routes = [];
                grouped[address] = routes;
            }

            routes.Add(route);
        }

        foreach (ResourceAddress address in this.Ordering)
        {
            // An ordering dependency on something already routed from adds nothing; the route implies it.
            if (!grouped.ContainsKey(address))
            {
                grouped[address] = [];
            }
        }

        return [.. grouped
            .OrderBy(static entry => entry.Key.KindName, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.Identifier, StringComparer.Ordinal)
            .Select(static entry => new Connection(entry.Key.KindName, entry.Key.Identifier, entry.Value))];
    }
}

/// <summary>
/// A resource the run brings up and takes down again.
/// </summary>
/// <remarks>
/// Only these produce lifecycle work, which is what keeps a run that provisions nothing exactly as cheap
/// as it is today: relayed configuration nodes carry no creation and no teardown.
/// </remarks>
public abstract class ProvisionedResourceNode : ResourceNode
{
    /// <summary>
    /// Brings the resource up and publishes what it now holds.
    /// </summary>
    /// <param name="context">Its declared neighbours, the channel to publish values on, and the run's services.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>State later nodes may need, or null when it has none.</returns>
    public abstract Task<object?> CreateAsync(NodeContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the resource down again.
    /// </summary>
    /// <remarks>
    /// The values it published are withdrawn around this call, so nothing afterwards can dial a
    /// coordinate that has stopped answering.
    /// </remarks>
    /// <param name="state">Whatever creation returned.</param>
    /// <param name="context">The same context creation received.</param>
    /// <param name="cancellationToken">Cancels the teardown.</param>
    public abstract Task DeconstructAsync(object? state, NodeContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Everything a node may use while it is created, handed to it rather than fetched from ambient state.
/// </summary>
public sealed class NodeContext
{
    private readonly ResourceValueStore values;
    private readonly ResourceNode node;

    internal NodeContext(
        ResourceNode node,
        ConnectionSet connections,
        ResourceValueStore values,
        IServiceProvider services,
        ScopedLogger logger)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        this.node = node;
        this.values = values;
        this.Connections = connections;
        this.Services = services;
        this.Logger = logger;
    }

    /// <summary>The nodes this one declared a connection to. Nothing else is reachable.</summary>
    public ConnectionSet Connections { get; }

    /// <summary>
    /// The run's registered services - factories, pools, options. Never run data: that arrives here.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>The scoped logger for the run.</summary>
    public ScopedLogger Logger { get; }

    /// <summary>
    /// Publishes a value this node now holds, for one viewpoint.
    /// </summary>
    /// <remarks>
    /// Both viewpoints are published by the node that knows them - it holds the container and the network
    /// alias - rather than one being derived from the other afterwards, which is what made connection
    /// strings something to be parsed and patched.
    /// </remarks>
    /// <param name="valueName">Which value, from the node's own kind.</param>
    /// <param name="vantage">Whose viewpoint this particular value is built for.</param>
    /// <param name="value">The value.</param>
    public void Produce(string valueName, ResourceVantage vantage, string value)
        => this.Publish(valueName, vantage, value);

    /// <summary>
    /// Publishes a value that reads the same from every viewpoint, such as a name.
    /// </summary>
    /// <param name="valueName">Which value, from the node's own kind.</param>
    /// <param name="value">The value.</param>
    public void Produce(string valueName, string value)
        => this.Publish(valueName, vantage: null, value);

    private void Publish(string valueName, ResourceVantage? vantage, string value)
    {
        // The kind is the promise plan-time validation checked routes against; producing something
        // outside it would make that validation a promise the run does not keep.
        if (!this.node.Kind.TryGetValue(valueName, out ResourceValue? offered))
        {
            throw new Exceptions.FrameworkConfigurationException(
                $"{this.node} produced '{valueName}', which {this.node.Kind} does not offer.",
                ["Declare the value on the kind, so a route to it can be checked before anything starts."],
                [.. this.node.Kind.Values.Select(static value => value.ToString())]);
        }

        if (offered!.PerVantage && vantage is null)
        {
            throw new Exceptions.FrameworkConfigurationException(
                $"{this.node} produced '{valueName}' without a viewpoint, but {this.node.Kind} offers it per viewpoint.",
                ["Publish it once for each viewpoint: what the test process reaches is not what a peer container reaches."],
                []);
        }

        if (!offered.PerVantage && vantage is not null)
        {
            throw new Exceptions.FrameworkConfigurationException(
                $"{this.node} produced '{valueName}' for {vantage}, but {this.node.Kind} offers it as one value for every viewpoint.",
                ["Publish it once, without a viewpoint."],
                []);
        }

        this.values.Produce(
            this.node.KindName,
            this.node.Identifier,
            offered.KeyFor(vantage ?? ResourceVantage.Host),
            value,
            this.node.ToString());
    }
}
