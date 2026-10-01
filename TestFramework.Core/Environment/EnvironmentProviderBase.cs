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
    private readonly Dictionary<string, HashSet<EnvComponentIdentifier>> _declaredResourceMappings = [];

    /// <summary>
    /// Gets a value indicating whether dependency-ready environment components may be created in parallel.
    /// </summary>
    public virtual bool SupportsParallelComponentCreation => false;

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
    /// Maps a resource kind to the component that stands in for <em>every</em> resource of that kind in a
    /// run - an emulator taking the place of a whole service. Every requirement of the kind reaches this
    /// environment, whoever declared the resource.
    /// </summary>
    protected void MapResourceKind(string resourceKind, EnvComponentIdentifier componentIdentifier)
    {
        Map(_resourceKindMappings, resourceKind, componentIdentifier);
    }

    /// <summary>
    /// Maps a resource kind to the component that provides the resources of that kind <em>this environment
    /// declares</em> - one container per declared definition. A requirement for a resource of the kind that
    /// something else declared never reaches this environment, so a run can reach one API in a container
    /// and another deployed somewhere.
    /// </summary>
    /// <remarks>
    /// The environment declares those resources by implementing <see cref="Graph.IResourceNodeSource"/>, so
    /// they are on the run's resource list before anything starts.
    /// </remarks>
    protected void MapDeclaredResources(string resourceKind, EnvComponentIdentifier componentIdentifier)
    {
        Map(_declaredResourceMappings, resourceKind, componentIdentifier);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Yes exactly for the kinds mapped with <see cref="MapResourceKind"/>; a kind mapped with
    /// <see cref="MapDeclaredResources"/> is served only for what this environment declares.
    /// </remarks>
    public virtual bool ProvidesEveryResourceOf(string resourceKind)
    {
        return _resourceKindMappings.ContainsKey(resourceKind);
    }

    private static void Map(Dictionary<string, HashSet<EnvComponentIdentifier>> mappings, string resourceKind, EnvComponentIdentifier componentIdentifier)
    {
        if (!mappings.TryGetValue(resourceKind, out HashSet<EnvComponentIdentifier>? mappedComponents))
        {
            mappedComponents = [];
            mappings[resourceKind] = mappedComponents;
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
            if (_resourceKindMappings.TryGetValue(requirement.ResourceKind, out HashSet<EnvComponentIdentifier>? mappedComponents))
                resolved.UnionWith(mappedComponents);
            if (_declaredResourceMappings.TryGetValue(requirement.ResourceKind, out HashSet<EnvComponentIdentifier>? declaredComponents))
                resolved.UnionWith(declaredComponents);
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
}