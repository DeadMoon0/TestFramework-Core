using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment;

/// <summary>
/// Represents one dependency edge in a resolved component graph.
/// </summary>
public sealed record ComponentGraphDependency
{
    /// <summary>
    /// The dependent component type.
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// The realized identity of the dependency instance.
    /// </summary>
    public required string RealizedComponentIdentity { get; init; }

    /// <summary>
    /// Whether the dependency may be shared or must remain isolated.
    /// </summary>
    public DependencyOwnership Ownership { get; init; } = DependencyOwnership.Shared;
}

/// <summary>
/// Represents one resolved component node used for graph validation and contract binding.
/// </summary>
public sealed record ComponentGraphNode
{
    /// <summary>
    /// The concrete component type.
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// The realized identity of this component instance.
    /// </summary>
    public required string RealizedComponentIdentity { get; init; }

    /// <summary>
    /// Resolved dependency edges for this component.
    /// </summary>
    public required IReadOnlyCollection<ComponentGraphDependency> Dependencies { get; init; }

    /// <summary>
    /// Contracts this component provides.
    /// </summary>
    public required IReadOnlyCollection<IEnvironmentResourceContract> Provides { get; init; }

    /// <summary>
    /// Contracts this component requires.
    /// </summary>
    public required IReadOnlyCollection<IEnvironmentResourceContract> Requires { get; init; }
}

/// <summary>
/// Represents a successful binding between a required contract and a providing component.
/// </summary>
public sealed record ComponentContractBinding
{
    internal ComponentContractBinding(
        Type consumerComponentType,
        string consumerIdentity,
        IEnvironmentResourceContract requirement,
        Type providerComponentType,
        string providerIdentity,
        IEnvironmentResourceContract providerContract)
    {
        this.ConsumerComponentType = consumerComponentType;
        this.ConsumerIdentity = consumerIdentity;
        this.Requirement = requirement;
        this.ProviderComponentType = providerComponentType;
        this.ProviderIdentity = providerIdentity;
        this.ProviderContract = providerContract;
    }

    /// <summary>
    /// The consuming component type.
    /// </summary>
    public Type ConsumerComponentType { get; internal init; }

    /// <summary>
    /// The realized identity of the consuming component.
    /// </summary>
    public string ConsumerIdentity { get; internal init; }

    /// <summary>
    /// The required contract.
    /// </summary>
    public IEnvironmentResourceContract Requirement { get; internal init; }

    /// <summary>
    /// The providing component type.
    /// </summary>
    public Type ProviderComponentType { get; internal init; }

    /// <summary>
    /// The realized identity of the providing component.
    /// </summary>
    public string ProviderIdentity { get; internal init; }

    /// <summary>
    /// The matched provider contract.
    /// </summary>
    public IEnvironmentResourceContract ProviderContract { get; internal init; }
}

/// <summary>
/// Validates resolved component graphs before any instances are created.
/// </summary>
public static class ComponentGraphValidator
{
    /// <summary>
    /// Validates a resolved graph for missing dependencies, cyclic dependencies, and exclusive ownership conflicts.
    /// </summary>
    /// <param name="nodes">The resolved graph nodes.</param>
    public static void Validate(IEnumerable<ComponentGraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        ComponentGraphNode[] graphNodes = nodes.ToArray();
        Dictionary<string, ComponentGraphNode> nodesByIdentity = graphNodes.ToDictionary(x => x.RealizedComponentIdentity, StringComparer.Ordinal);

        ValidateDependenciesExist(graphNodes, nodesByIdentity);
        ValidateNoCycles(graphNodes, nodesByIdentity);
        ValidateExclusiveOwnership(graphNodes);
    }

    private static void ValidateDependenciesExist(ComponentGraphNode[] nodes, Dictionary<string, ComponentGraphNode> nodesByIdentity)
    {
        foreach (ComponentGraphNode node in nodes)
        {
            foreach (ComponentGraphDependency dependency in node.Dependencies)
            {
                if (!nodesByIdentity.ContainsKey(dependency.RealizedComponentIdentity))
                {
                    throw new DependencyGraphException(
                        $"Component '{node.RealizedComponentIdentity}' depends on '{dependency.RealizedComponentIdentity}', but no matching component node was resolved.");
                }
            }
        }
    }

    private static void ValidateNoCycles(ComponentGraphNode[] nodes, Dictionary<string, ComponentGraphNode> nodesByIdentity)
    {
        HashSet<string> visiting = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);

        foreach (ComponentGraphNode node in nodes)
            Visit(node.RealizedComponentIdentity, nodesByIdentity, visiting, visited);
    }

    private static void Visit(string identity, Dictionary<string, ComponentGraphNode> nodesByIdentity, HashSet<string> visiting, HashSet<string> visited)
    {
        if (visited.Contains(identity))
            return;

        if (!visiting.Add(identity))
            throw new DependencyGraphException($"A cyclic component dependency was detected at '{identity}'.");

        foreach (ComponentGraphDependency dependency in nodesByIdentity[identity].Dependencies)
            Visit(dependency.RealizedComponentIdentity, nodesByIdentity, visiting, visited);

        visiting.Remove(identity);
        visited.Add(identity);
    }

    private static void ValidateExclusiveOwnership(ComponentGraphNode[] nodes)
    {
        var exclusiveGroups = nodes
            .SelectMany(node => node.Dependencies.Select(dependency => new
            {
                Owner = node.RealizedComponentIdentity,
                dependency.RealizedComponentIdentity,
                dependency.Ownership
            }))
            .Where(x => x.Ownership == DependencyOwnership.Exclusive)
            .GroupBy(x => x.RealizedComponentIdentity, StringComparer.Ordinal);

        foreach (var group in exclusiveGroups)
        {
            string[] owners = group.Select(x => x.Owner).Distinct(StringComparer.Ordinal).ToArray();
            if (owners.Length > 1)
            {
                throw new DependencyGraphException(
                    $"Multiple exclusive dependencies resolved to the same realized component identity '{group.Key}'. Owners: {string.Join(", ", owners)}.");
            }
        }
    }
}

/// <summary>
/// Resolves required contracts against compatible provider contracts within a resolved component graph.
/// </summary>
public static class ContractBindingPass
{
    /// <summary>
    /// Binds required contracts to exactly one compatible provider contract.
    /// </summary>
    /// <param name="nodes">The resolved graph nodes.</param>
    /// <param name="isMatch">Determines whether a provider contract satisfies a required contract.</param>
    /// <returns>The successful bindings.</returns>
    public static IReadOnlyCollection<ComponentContractBinding> Bind(
        IEnumerable<ComponentGraphNode> nodes,
        Func<IEnvironmentResourceContract, IEnvironmentResourceContract, bool> isMatch)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(isMatch);

        ComponentGraphNode[] graphNodes = nodes.ToArray();
        List<ComponentContractBinding> bindings = [];

        foreach (ComponentGraphNode consumer in graphNodes)
        {
            foreach (IEnvironmentResourceContract requirement in consumer.Requires)
            {
                ComponentContractBinding[] compatibleProviders = graphNodes
                    .SelectMany(provider => provider.Provides
                        .Where(contract => isMatch(requirement, contract))
                        .Select(contract => new ComponentContractBinding(
                            consumer.ComponentType,
                            consumer.RealizedComponentIdentity,
                            requirement,
                            provider.ComponentType,
                            provider.RealizedComponentIdentity,
                            contract)))
                    .ToArray();

                if (compatibleProviders.Length == 0)
                {
                    throw new DependencyGraphException(
                        $"Component '{consumer.RealizedComponentIdentity}' requires '{requirement}', but no compatible provider was found.");
                }

                if (compatibleProviders.Length > 1)
                {
                    string[] providers = compatibleProviders
                        .Select(x => x.ProviderIdentity)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    throw new DependencyGraphException(
                        $"Component '{consumer.RealizedComponentIdentity}' requires '{requirement}', but multiple compatible providers were found: {string.Join(", ", providers)}.");
                }

                bindings.Add(compatibleProviders[0]);
            }
        }

        return bindings;
    }
}
