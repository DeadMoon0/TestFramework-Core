using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Environment;

/// <summary>
/// Provides a base implementation for mapping artifacts and resource requirements to environment components.
/// </summary>
public abstract class EnvironmentProviderBase : IEnvironmentProvider
{
    private readonly Dictionary<EnvComponentIdentifier, EnvComponent> _components = [];
    private readonly Dictionary<Type, HashSet<EnvComponentIdentifier>> _artifactMappings = [];
    private readonly Dictionary<string, HashSet<EnvComponentIdentifier>> _resourceKindMappings = [];

    /// <summary>
    /// Registers an environment component.
    /// </summary>
    protected void AddComponent(EnvComponent component)
    {
        _components[component.Id] = component;
    }

    /// <summary>
    /// Maps an artifact describer type to an environment component.
    /// </summary>
    protected void MapArtifact<TArtifactDescriber>(EnvComponentIdentifier componentIdentifier)
        where TArtifactDescriber : ArtifactDescriberGeneric
    {
        MapArtifact(typeof(TArtifactDescriber), componentIdentifier);
    }

    /// <summary>
    /// Maps an artifact describer CLR type to an environment component.
    /// </summary>
    protected void MapArtifact(Type artifactDescriberType, EnvComponentIdentifier componentIdentifier)
    {
        if (!_artifactMappings.TryGetValue(artifactDescriberType, out HashSet<EnvComponentIdentifier>? mappedComponents))
        {
            mappedComponents = [];
            _artifactMappings[artifactDescriberType] = mappedComponents;
        }

        mappedComponents.Add(componentIdentifier);
    }

    /// <summary>
    /// Maps a resource kind string to an environment component.
    /// </summary>
    protected void MapResourceKind(string resourceKind, EnvComponentIdentifier componentIdentifier)
    {
        if (!_resourceKindMappings.TryGetValue(resourceKind, out HashSet<EnvComponentIdentifier>? mappedComponents))
        {
            mappedComponents = [];
            _resourceKindMappings[resourceKind] = mappedComponents;
        }

        mappedComponents.Add(componentIdentifier);
    }

    /// <summary>
    /// Resolves the set of environment components required by the provided artifacts and environment requirements.
    /// </summary>
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

    /// <summary>
    /// Gets a registered environment component by identifier.
    /// </summary>
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

    /// <summary>
    /// Allows derived providers to react when a requirement is resolved.
    /// </summary>
    protected virtual void OnRequirementResolved(EnvironmentRequirement requirement)
    {
    }
}