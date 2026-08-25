using System;
using System.Collections;
using System.Collections.Generic;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// One run's resources: what was declared, whether it holds together, and where things ended up.
/// </summary>
/// <remarks>
/// <para>
/// Composed once per run, before anything starts, so a contradiction in what a test declared is a failure
/// with a message instead of a step timing out against an address nobody supplied. Every value a step can
/// read arrives through this, which is what makes <c>Values.Require</c> the single answer to "where is this
/// resource" rather than one of several ways to guess.
/// </para>
/// <para>
/// Sources arrive the way every other piece does - as services, plus the environment when it is one -
/// because a run should not have to know whether its resources came from a configuration file, a fixture
/// or a container.
/// </para>
/// </remarks>
internal sealed class RunResources
{
    private RunResources(ResourceGraph graph, ResourceValueStore values)
    {
        this.Graph = graph;
        this.Values = values;
        this.Resolution = new ValueResolution(values);
    }

    /// <summary>What this run declared, validated.</summary>
    internal ResourceGraph Graph { get; }

    /// <summary>The values themselves. Produced values land here as nodes start.</summary>
    internal ResourceValueStore Values { get; }

    /// <summary>How anything in the run asks for one.</summary>
    internal ValueResolution Resolution { get; }

    /// <summary>
    /// Composes and validates the run's resources.
    /// </summary>
    /// <remarks>
    /// A run with no sources gets an empty graph rather than a special case: reading a value it does not
    /// have then fails by saying nothing in this run supplies it, which is the truth and is the same
    /// message a misdeclared resource produces.
    /// </remarks>
    /// <param name="serviceProvider">The run's services, which may register sources.</param>
    /// <param name="environment">The run's environment, when it is also a source.</param>
    /// <returns>The run's resources.</returns>
    internal static RunResources Compose(IServiceProvider? serviceProvider, object? environment)
    {
        List<IResourceNodeSource> sources = [];

        // The environment first: when it declares resources, those are the run's own, and a registered
        // source contributing the same identifier is the one that gets overridden rather than the reverse.
        Add(sources, environment as IResourceNodeSource);

        if (serviceProvider is not null)
        {
            if (serviceProvider.GetService(typeof(IEnumerable<IResourceNodeSource>)) is IEnumerable registered)
            {
                foreach (object? candidate in registered)
                {
                    Add(sources, candidate as IResourceNodeSource);
                }
            }

            Add(sources, serviceProvider.GetService(typeof(IResourceNodeSource)) as IResourceNodeSource);
        }

        ResourceGraph graph = ResourceGraph.Compose(sources);

        // Plan time, before a single step runs: a route to a value nothing offers, or two resources that
        // wait for each other, is a sentence rather than a hang.
        graph.Validate();

        ResourceValueStore values = new ResourceValueStore();

        foreach (ResourceNode node in graph.Nodes)
        {
            foreach (KeyValuePair<ValueKey, string> declared in node.DeclaredValues)
            {
                values.Declare(node.KindName, node.Identifier, declared.Key, declared.Value, graph.ProviderOf(node));
            }
        }

        return new RunResources(graph, values);
    }

    private static void Add(List<IResourceNodeSource> sources, IResourceNodeSource? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        foreach (IResourceNodeSource known in sources)
        {
            if (ReferenceEquals(known, candidate))
            {
                return;
            }
        }

        sources.Add(candidate);
    }
}
