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

        // The environment first, so it loses to a registered source naming the same resource. That is the
        // order a fallback wants: an environment declares what a resource is when nobody wrote it down -
        // the database a definition names, say - and somebody who did write it down meant it. Where an
        // environment does override a file is at run time, by publishing, which is a different mechanism
        // and answers a different question: not what this resource is, but where it ended up.
        AddEnvironment(sources, environment);

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
                values.Declare(node.KindName, node.Identifier, declared.Key, declared.Value, graph.ProviderOf(node), node.Kind.IsSecret(declared.Key.ValueName));
            }
        }

        return new RunResources(graph, values);
    }

    /// <summary>
    /// Adds the environment's own declarations, reaching through any wrappers around it.
    /// </summary>
    /// <remarks>
    /// A run rarely holds the environment somebody wrote: a persistent slice wraps it to hand back
    /// containers it already started, and a hosted fixture wraps that again to carry the run's services.
    /// Asking only the outermost object whether it declares resources means the environments that do
    /// declare them are exactly the ones never asked, and the failure is silent - an empty graph reads
    /// the same as an environment that declares nothing.
    /// </remarks>
    /// <param name="sources">The sources being collected.</param>
    /// <param name="environment">The run's environment, wrapped or not.</param>
    private static void AddEnvironment(List<IResourceNodeSource> sources, object? environment)
    {
        List<IResourceNodeSource> chain = [];

        for (object? current = environment; current is not null;)
        {
            if (current is IResourceNodeSource source)
            {
                chain.Add(source);
            }

            current = current is IEnvironmentProviderProxy proxy ? proxy.InnerEnvironment : null;
        }

        // Innermost first, so a wrapper that declares something outranks what it wraps - the same rule as
        // everywhere else here, that the piece closer to this run has the last word.
        for (int index = chain.Count - 1; index >= 0; index--)
        {
            Add(sources, chain[index]);
        }
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
