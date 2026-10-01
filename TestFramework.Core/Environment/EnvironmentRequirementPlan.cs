using System.Collections.Generic;
using System.Linq;

using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

/// <summary>
/// One requirement of the run, the step that stated it, and the resource it names.
/// </summary>
/// <param name="Step">The step that needs it.</param>
/// <param name="Requirement">What the step stated.</param>
/// <param name="Node">The resource on the run's list it names.</param>
internal sealed record PlannedRequirement(StepGeneric Step, EnvironmentRequirement Requirement, ResourceNode Node);

/// <summary>
/// What a run's steps need, checked against what the run declares - before anything starts - and handed to
/// the environment that is to provide it.
/// </summary>
/// <remarks>
/// <para>
/// The rule lives here and nowhere else. Environments used to hold their own versions of it: one refused a
/// name no definition of its declared even when configuration named it, another threw for a whole kind it
/// did not host, and without an environment nothing checked at all, so a missing resource surfaced only
/// when a step went to read it. One rule, stated once, before the first step: every requirement names a
/// resource on the run's list, whoever declared it.
/// </para>
/// <para>
/// A run without an environment stays legal. It only has to need nothing its configuration and fixtures do
/// not declare.
/// </para>
/// </remarks>
internal static class EnvironmentRequirementPlan
{
    /// <summary>
    /// Collects every step's requirements with the run's resources in view, refusing the first that names
    /// no resource on the list.
    /// </summary>
    /// <param name="steps">The main stage's steps.</param>
    /// <param name="variables">The run's variables.</param>
    /// <param name="resources">What the run declares.</param>
    /// <returns>The requirements, each with the resource it names.</returns>
    /// <exception cref="FrameworkConfigurationException">A requirement names nothing the run declares.</exception>
    internal static IReadOnlyList<PlannedRequirement> Check(IEnumerable<StepGeneric> steps, VariableStore variables, ResourceGraph resources)
    {
        List<PlannedRequirement> planned = [];
        foreach (StepGeneric step in steps)
        {
            if (step is not IHasEnvironmentRequirements provider)
            {
                continue;
            }

            foreach (EnvironmentRequirement requirement in provider.GetEnvironmentRequirements(variables, resources))
            {
                planned.Add(new PlannedRequirement(step, requirement, Resolve(step, requirement, resources)));
            }
        }

        return planned;
    }

    /// <summary>
    /// The requirements that are one environment's to provide: those for a resource it declared itself, and
    /// those of a kind it stands in for entirely. Each is stated with the kind it resolved to, so an
    /// environment never has to resolve a kind-agnostic name itself.
    /// </summary>
    /// <param name="environment">The run's environment.</param>
    /// <param name="planned">The checked requirements.</param>
    /// <param name="resources">What the run declares.</param>
    /// <param name="environmentSources">What the environment declares its resources through.</param>
    /// <returns>The environment's requirements.</returns>
    internal static IReadOnlyCollection<EnvironmentRequirement> RoutedTo(
        IEnvironmentProvider environment,
        IReadOnlyList<PlannedRequirement> planned,
        ResourceGraph resources,
        IReadOnlyCollection<IResourceNodeSource> environmentSources)
    {
        return [.. planned
            .Where(requirement => resources.IsDeclaredByAny(requirement.Node, environmentSources)
                || environment.ProvidesEveryResourceOf(requirement.Node.KindName))
            .Select(static requirement => new EnvironmentRequirement(requirement.Node.KindName, requirement.Node.Identifier))
            .Distinct()];
    }

    private static ResourceNode Resolve(StepGeneric step, EnvironmentRequirement requirement, ResourceGraph resources)
    {
        if (!EnvironmentResourceKinds.IsAny(requirement.ResourceKind))
        {
            return resources.TryGetNode(requirement.ResourceKind, requirement.ResourceIdentifier, out ResourceNode? node)
                ? node!
                : throw Missing(step, requirement, [.. resources.Nodes.Where(candidate => candidate.KindName == requirement.ResourceKind)], resources);
        }

        IReadOnlyList<ResourceNode> named = resources.NodesNamed(requirement.ResourceIdentifier);
        if (named.Count > 1)
        {
            throw new FrameworkConfigurationException(
                $"Step '{LabelOf(step)}' requires '{requirement.ResourceIdentifier}' without saying which kind, and {named.Count} kinds of resource carry that name.")
            {
                RecoverySteps = ["Require the kind as well, or rename one of the resources."],
                AvailableOptions = [.. named.Select(node => Describe(node, resources))],
            };
        }

        return named.Count == 1
            ? named[0]
            : throw Missing(step, requirement, resources.Nodes, resources);
    }

    private static FrameworkConfigurationException Missing(StepGeneric step, EnvironmentRequirement requirement, IReadOnlyList<ResourceNode> candidates, ResourceGraph resources)
    {
        string what = EnvironmentResourceKinds.IsAny(requirement.ResourceKind)
            ? $"a resource named '{requirement.ResourceIdentifier}'"
            : $"{requirement.ResourceKind} '{requirement.ResourceIdentifier}'";

        return new FrameworkConfigurationException(
            $"Step '{LabelOf(step)}' requires {what}, and nothing in this run declares it.")
        {
            RecoverySteps = [
                "Declare it in the configuration this run is set up with, or include the definition that provides it in the environment set with SetEnv(...).",
                "Resource names are case-sensitive.",
            ],
            AvailableOptions = [.. candidates.Select(node => Describe(node, resources))],
        };
    }

    private static string Describe(ResourceNode node, ResourceGraph resources)
        => $"{node.KindName} '{node.Identifier}' ({resources.ProviderOf(node)})";

    private static string LabelOf(StepGeneric step)
        => string.IsNullOrWhiteSpace(step.LabelOptions.Label) ? step.Name : step.LabelOptions.Label!;
}
