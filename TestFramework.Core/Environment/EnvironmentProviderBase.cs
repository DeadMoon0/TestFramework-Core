using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Environment;

public abstract class EnvironmentProviderBase : IEnvironmentProvider
{
    private readonly Dictionary<EnvComponentIdentifier, EnvComponent> _components = [];
    private readonly Dictionary<Type, HashSet<EnvComponentIdentifier>> _artifactMappings = [];
    private readonly Dictionary<string, HashSet<EnvComponentIdentifier>> _resourceKindMappings = [];

    protected void AddComponent(EnvComponent component)
    {
        _components[component.Id] = component;
    }

    protected void MapArtifact<TArtifactDescriber>(EnvComponentIdentifier componentIdentifier)
        where TArtifactDescriber : ArtifactDescriberGeneric
    {
        MapArtifact(typeof(TArtifactDescriber), componentIdentifier);
    }

    protected void MapArtifact(Type artifactDescriberType, EnvComponentIdentifier componentIdentifier)
    {
        if (!_artifactMappings.TryGetValue(artifactDescriberType, out HashSet<EnvComponentIdentifier>? mappedComponents))
        {
            mappedComponents = [];
            _artifactMappings[artifactDescriberType] = mappedComponents;
        }

        mappedComponents.Add(componentIdentifier);
    }

    protected void MapResourceKind(string resourceKind, EnvComponentIdentifier componentIdentifier)
    {
        if (!_resourceKindMappings.TryGetValue(resourceKind, out HashSet<EnvComponentIdentifier>? mappedComponents))
        {
            mappedComponents = [];
            _resourceKindMappings[resourceKind] = mappedComponents;
        }

        mappedComponents.Add(componentIdentifier);
    }

    public virtual IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements)
    {
        HashSet<EnvComponentIdentifier> resolved = [];

        foreach (ArtifactInstanceGeneric artifact in artifacts)
        {
            Type artifactType = artifact.Artifact.GetType();
            foreach ((Type mappedType, HashSet<EnvComponentIdentifier> componentIds) in _artifactMappings)
            {
                if (!MatchesArtifactType(mappedType, artifactType))
                    continue;

                resolved.UnionWith(componentIds);
            }
        }

        foreach (EnvironmentRequirement requirement in requirements)
        {
            OnRequirementResolved(requirement);
            if (_resourceKindMappings.TryGetValue(requirement.ResourceKind, out HashSet<EnvComponentIdentifier>? mappedComponents))
                resolved.UnionWith(mappedComponents);
        }

        return [.. resolved];
    }

    public EnvComponent GetComponent(EnvComponentIdentifier identifier)
    {
        if (_components.TryGetValue(identifier, out EnvComponent? component))
            return component;

        throw new KeyNotFoundException($"No environment component with identifier '{identifier}' was registered.");
    }

    private static bool MatchesArtifactType(Type mappedType, Type artifactType)
    {
        if (mappedType.IsAssignableFrom(artifactType))
            return true;

        if (!mappedType.IsGenericTypeDefinition)
            return false;

        if (artifactType.IsGenericType && artifactType.GetGenericTypeDefinition() == mappedType)
            return true;

        return artifactType.GetInterfaces()
            .Where(type => type.IsGenericType)
            .Select(type => type.GetGenericTypeDefinition())
            .Contains(mappedType);
    }

    protected virtual void OnRequirementResolved(EnvironmentRequirement requirement)
    {
    }
}