using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Everything this run's resources are, how they connect, and in what order they may be brought up.
/// </summary>
/// <remarks>
/// <para>
/// Composed from the run's environments in order, the relayed configuration first. Later environments
/// shadow earlier ones per node, so pointing a run at containers overrides the addresses a file
/// declared without either side knowing about the other - one rule instead of the opposite conventions
/// that used to live in different packages.
/// </para>
/// <para>
/// The graph is a plan-time object. It answers what exists, what needs what, what is reachable and what
/// order creation takes - all before anything starts, which is what lets a mistyped identifier fail
/// while it is still free to fail.
/// </para>
/// </remarks>
public sealed class ResourceGraph
{
    private readonly Dictionary<NodeId, ResourceNode> nodes;
    private readonly Dictionary<NodeId, string> providers;

    private ResourceGraph(Dictionary<NodeId, ResourceNode> nodes, Dictionary<NodeId, string> providers)
    {
        this.nodes = nodes;
        this.providers = providers;
    }

    /// <summary>
    /// Composes the run's graph.
    /// </summary>
    /// <param name="sources">The node sources in precedence order - earliest first, so the last one supplying a node wins.</param>
    /// <returns>The graph.</returns>
    public static ResourceGraph Compose(IReadOnlyList<IResourceNodeSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        Dictionary<NodeId, ResourceNode> nodes = [];
        Dictionary<NodeId, string> providers = [];

        foreach (IResourceNodeSource source in sources)
        {
            foreach (ResourceNode node in source.Nodes)
            {
                NodeId id = NodeId.Of(node);
                nodes[id] = node;
                providers[id] = source.SourceName;
            }
        }

        return new ResourceGraph(nodes, providers);
    }

    /// <summary>Every node in the run, ordered so two runs of one test read the same.</summary>
    public IReadOnlyList<ResourceNode> Nodes
        => [.. this.nodes.Values
            .OrderBy(static node => node.KindName, StringComparer.Ordinal)
            .ThenBy(static node => node.Identifier, StringComparer.Ordinal)];

    /// <summary>Who supplied a node, for messages and the run's log line.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The source name.</returns>
    public string ProviderOf(ResourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return this.providers.TryGetValue(NodeId.Of(node), out string? provider) ? provider : "unknown";
    }

    /// <summary>
    /// Finds a node by kind and identifier.
    /// </summary>
    /// <param name="resourceKind">The kind.</param>
    /// <param name="identifier">Which one.</param>
    /// <param name="node">The node, when the graph has it.</param>
    /// <returns>True when the graph has it.</returns>
    public bool TryGetNode(string resourceKind, string identifier, out ResourceNode? node)
        => this.nodes.TryGetValue(new NodeId(resourceKind, identifier), out node);

    /// <summary>
    /// Finds a node named without its kind, for a consumer that is kind-agnostic by design.
    /// </summary>
    /// <param name="identifier">Which one.</param>
    /// <param name="node">The node, when exactly one kind answers.</param>
    /// <returns>True when exactly one kind answers.</returns>
    /// <exception cref="FrameworkConfigurationException">More than one kind answers to the identifier.</exception>
    public bool TryGetNode(string identifier, out ResourceNode? node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        List<ResourceNode> matches = [.. this.nodes
            .Where(entry => string.Equals(entry.Key.Identifier, identifier, StringComparison.Ordinal))
            .Select(static entry => entry.Value)];

        if (matches.Count > 1)
        {
            throw new FrameworkConfigurationException(
                $"'{identifier}' names {matches.Count} different kinds of resource in this run.",
                ["Say which kind, or rename one of them."],
                [.. matches.Select(match => $"{match} from {this.ProviderOf(match)}")]);
        }

        node = matches.Count == 1 ? matches[0] : null;

        return node is not null;
    }

    /// <summary>
    /// Checks that every connection in the graph can be satisfied, and that nothing depends on itself.
    /// </summary>
    /// <remarks>
    /// Runs before any provisioning. A missing neighbour or a cycle costs a readable message and no
    /// containers, instead of a half-built environment and a puzzle.
    /// </remarks>
    /// <exception cref="FrameworkConfigurationException">A connection names a node the graph does not have, or a cycle exists.</exception>
    public void Validate()
    {
        foreach (ResourceNode node in this.Nodes)
        {
            foreach (Connection connection in node.Connections)
            {
                if (!this.nodes.TryGetValue(new NodeId(connection.KindName, connection.Identifier), out ResourceNode? neighbour))
                {
                    throw this.Unsatisfied(node, connection);
                }

                foreach (ValueRoute route in connection.Routes)
                {
                    // The neighbour exists; does it offer what the route asks of it? Asking a database
                    // for an address is an authoring mistake, and this is where it costs a message
                    // instead of a half-built environment.
                    if (!Offers(neighbour, route))
                    {
                        throw NotOffered(node, neighbour, route);
                    }
                }
            }
        }

        this.CreationOrder(this.Nodes);
    }

    /// <summary>
    /// The nodes reachable from what the run actually uses.
    /// </summary>
    /// <remarks>
    /// A graph may describe more than a run needs - a solution declares every resource it has, one test
    /// touches three of them. Reachability is what keeps the rest unprovisioned, and it follows
    /// connections, so a node needed only as somebody else's neighbour comes along.
    /// </remarks>
    /// <param name="roots">The kinds and identifiers the run's steps and artifacts named.</param>
    /// <returns>The reachable nodes, in no particular order.</returns>
    public IReadOnlyList<ResourceNode> Reachable(IEnumerable<EnvironmentRequirement> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        HashSet<NodeId> seen = [];
        Queue<NodeId> pending = new Queue<NodeId>();

        foreach (EnvironmentRequirement requirement in roots)
        {
            foreach (NodeId id in this.Resolve(requirement))
            {
                if (seen.Add(id))
                {
                    pending.Enqueue(id);
                }
            }
        }

        List<ResourceNode> reachable = [];

        while (pending.Count > 0)
        {
            NodeId id = pending.Dequeue();

            if (!this.nodes.TryGetValue(id, out ResourceNode? node))
            {
                continue;
            }

            reachable.Add(node);

            foreach (Connection connection in node.Connections)
            {
                NodeId neighbour = new NodeId(connection.KindName, connection.Identifier);

                if (seen.Add(neighbour))
                {
                    pending.Enqueue(neighbour);
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// The order the given nodes may be created in: every node after everything it connects to.
    /// </summary>
    /// <param name="nodes">The nodes to order.</param>
    /// <returns>The creation order.</returns>
    /// <exception cref="FrameworkConfigurationException">The connections form a cycle.</exception>
    public IReadOnlyList<ResourceNode> CreationOrder(IReadOnlyList<ResourceNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        HashSet<NodeId> included = [.. nodes.Select(NodeId.Of)];
        Dictionary<NodeId, ResourceNode> byId = nodes.ToDictionary(NodeId.Of, static node => node);

        List<ResourceNode> ordered = [];
        HashSet<NodeId> done = [];
        HashSet<NodeId> visiting = [];

        foreach (ResourceNode node in nodes
            .OrderBy(static node => node.KindName, StringComparer.Ordinal)
            .ThenBy(static node => node.Identifier, StringComparer.Ordinal))
        {
            Visit(node, []);
        }

        return ordered;

        void Visit(ResourceNode node, IReadOnlyList<ResourceNode> path)
        {
            NodeId id = NodeId.Of(node);

            if (done.Contains(id))
            {
                return;
            }

            if (!visiting.Add(id))
            {
                throw Cycle([.. path, node]);
            }

            foreach (Connection connection in node.Connections)
            {
                NodeId neighbour = new NodeId(connection.KindName, connection.Identifier);

                // Ordering only constrains what is actually being created; a connection to something
                // outside this set is validated elsewhere, not ordered here.
                if (included.Contains(neighbour) && byId.TryGetValue(neighbour, out ResourceNode? next))
                {
                    Visit(next, [.. path, node]);
                }
            }

            visiting.Remove(id);
            done.Add(id);
            ordered.Add(node);
        }
    }

    private IEnumerable<NodeId> Resolve(EnvironmentRequirement requirement)
    {
        if (!EnvironmentResourceKinds.IsAny(requirement.ResourceKind))
        {
            yield return new NodeId(requirement.ResourceKind, requirement.ResourceIdentifier);
            yield break;
        }

        // A kind-agnostic requirement is answered by the graph rather than by the asker: the browser
        // that says "storefront" should not have to know whether a site or an application serves it.
        foreach (NodeId id in this.nodes.Keys
            .Where(id => string.Equals(id.Identifier, requirement.ResourceIdentifier, StringComparison.Ordinal)))
        {
            yield return id;
        }
    }

    private FrameworkConfigurationException Unsatisfied(ResourceNode node, Connection connection)
        => new FrameworkConfigurationException(
            $"Nothing provides {connection.KindName}/{connection.Identifier}, needed by {node}"
                + (connection.Routes.Count == 0 ? "." : $" ({string.Join("; ", connection.Routes)})."),
            [
                "Include the definition that provisions it in the environment, or declare it in configuration.",
            ],
            [.. this.Nodes.Select(known => $"{known} from {this.ProviderOf(known)}")]);

    private static bool Offers(ResourceNode neighbour, ValueRoute route)
        => neighbour.Kind.Offers(route.Value.ValueName);

    private static FrameworkConfigurationException NotOffered(ResourceNode node, ResourceNode neighbour, ValueRoute route)
        => new FrameworkConfigurationException(
            $"{node} routes {route.Value.ValueName} ({route.Vantage}) from {neighbour}, which does not offer it.",
            ["Route a value the resource has, or declare this one on its kind."],
            [.. neighbour.Kind.Values.Select(offered => $"{neighbour.Kind} offers {offered.ValueName}")]);

    private static FrameworkConfigurationException Cycle(IReadOnlyList<ResourceNode> path)
        => new FrameworkConfigurationException(
            $"The resources depend on each other in a circle: {string.Join(" -> ", path.Select(static node => node.ToString()))}.",
            ["Break the circle: one of these connections has to go, or move the value it carries to a resource that comes earlier."],
            []);

    private readonly record struct NodeId(string ResourceKind, string Identifier)
    {
        public static NodeId Of(ResourceNode node) => new NodeId(node.KindName, node.Identifier);
    }
}

/// <summary>
/// Something that contributes nodes to a run's graph - an environment, or the relayed configuration.
/// </summary>
public interface IResourceNodeSource
{
    /// <summary>What to call this source in messages and logs.</summary>
    string SourceName { get; }

    /// <summary>The nodes it contributes.</summary>
    IReadOnlyList<ResourceNode> Nodes { get; }
}
