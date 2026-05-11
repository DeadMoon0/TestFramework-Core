using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Environment;

internal static class EnvComponentGraph
{
    internal static IReadOnlyList<EnvComponent> Order(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> rootComponents)
    {
        return Layers(environment, rootComponents).SelectMany(layer => layer).ToArray();
    }

    internal static IReadOnlyList<IReadOnlyList<EnvComponent>> Layers(IEnvironmentProvider environment, IEnumerable<EnvComponentIdentifier> rootComponents)
    {
        List<EnvComponent> ordered = [];
        HashSet<EnvComponentIdentifier> visiting = [];
        HashSet<EnvComponentIdentifier> visited = [];

        foreach (EnvComponentIdentifier rootComponent in rootComponents)
            Visit(environment, rootComponent, visiting, visited, ordered);

        List<IReadOnlyList<EnvComponent>> layers = [];
        HashSet<EnvComponentIdentifier> created = [];
        List<EnvComponent> pending = [.. ordered];

        while (pending.Count > 0)
        {
            EnvComponent[] layer = [.. pending.Where(component => component.Dependencies.All(created.Contains))];
            if (layer.Length == 0)
                throw new InvalidOperationException("Unable to resolve dependency-ready environment component layer.");

            layers.Add(layer);
            pending.RemoveAll(component => layer.Contains(component));
            foreach (EnvComponent component in layer)
                created.Add(component.Id);
        }

        return layers;
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