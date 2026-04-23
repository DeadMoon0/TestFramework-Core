using System;
using System.Collections.Generic;

namespace TestFramework.Core.Environment;

internal static class EnvComponentGraph
{
    internal static IReadOnlyList<EnvComponent> Order(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> rootComponents)
    {
        List<EnvComponent> ordered = [];
        HashSet<EnvComponentIdentifier> visiting = [];
        HashSet<EnvComponentIdentifier> visited = [];

        foreach (EnvComponentIdentifier rootComponent in rootComponents)
            Visit(environment, rootComponent, visiting, visited, ordered);

        return ordered;
    }

    private static void Visit(IEnvironmentProvider environment, EnvComponentIdentifier identifier, HashSet<EnvComponentIdentifier> visiting, HashSet<EnvComponentIdentifier> visited, List<EnvComponent> ordered)
    {
        if (visited.Contains(identifier))
            return;

        if (!visiting.Add(identifier))
            throw new InvalidOperationException($"A cyclic environment component dependency was detected at '{identifier}'.");

        EnvComponent component = environment.GetComponent(identifier);
        foreach (EnvComponentIdentifier dependency in component.Dependencies)
            Visit(environment, dependency, visiting, visited, ordered);

        visiting.Remove(identifier);
        visited.Add(identifier);
        ordered.Add(component);
    }
}